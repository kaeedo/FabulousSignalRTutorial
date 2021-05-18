﻿namespace Chat.Fabulous

open System
open System.IdentityModel.Tokens.Jwt
open System.Net.Http
open System.Threading.Tasks
open Fable.SignalR
open Fabulous
open Fabulous.XamarinForms
open Microsoft.Extensions.Logging
open Xamarin.Essentials
open Xamarin.Forms
open Microsoft.AspNetCore.SignalR.Client
open Fable.SignalR.Elmish
open Shared.SignalRHub

module App =
    // https://docs.microsoft.com/en-us/xamarin/essentials/web-authenticator?tabs=android
    // https://github.com/xamarin/Essentials/blob/develop/Samples/Sample.Server.WebAuthenticator/Controllers/MobileAuthController.cs
    let private serverUrl = "https://192.168.1.131:5001"
    //let private serverUrl = "https://10.193.16.71:5001"
    type Model =
        { Messages: string list
          EntryText: string
          Username: string
          Participants: string list
          Recipient: string
          IdToken: string
          Hub: Elmish.Hub<Action, Response> option }

    type Msg =
        | MessageEntryText of string
        | SendMessage
        | UsernameEntryText of string
        | EnterChatRoom
        | RecipientChanged of string
        | NoOp
        | RegisterHub of Elmish.Hub<Action, Response>
        | SignalRMessage of Response
        | Login
        | SetAuthResult of string
        | LoginFailed of exn

    // https://montemagno.com/real-time-communication-for-mobile-with-signalr/
    // https://nicksnettravels.builttoroam.com/android-certificates/
    let initModel =
        { Messages = []
          EntryText = String.Empty
          Hub = None
          IdToken = String.Empty
          Username = String.Empty
          Participants = []
          Recipient = String.Empty }

    let init () = initModel, Cmd.none

    let update msg model =
        match msg with
        | RegisterHub hub ->
            let hub = Some hub

            let cmd =
                Cmd.SignalR.send hub (Action.ClientConnected model.Username)

            { model with Hub = hub }, cmd
        | SignalRMessage response ->
            match response with
            | Response.ParticipantConnected (isAuthenticated, participant) ->
                let userConnectedMessage = sprintf "%s %s connected" (if not isAuthenticated then "(Guest)" else String.Empty) participant

                { model with
                      Messages = userConnectedMessage :: model.Messages
                      Participants = participant :: model.Participants },
                Cmd.none

            | Response.ReceiveDirectMessage (sender, message) ->
                let message =
                    (sprintf "%s whispered: %s" sender message)

                { model with
                      Messages = message :: model.Messages },
                Cmd.none
            | Response.ReceiveMessage message ->
                { model with
                      Messages = message :: model.Messages },
                Cmd.none
            | Response.Unauthorized message ->
                let display = async {
                    do! Application.Current.MainPage.DisplayAlert("Failed", message, "Ok") |> Async.AwaitTask
                    return None
                }

                model, Cmd.ofAsyncMsgOption display

        | SendMessage ->
            let cmd =
                if String.IsNullOrWhiteSpace(model.Recipient)
                   || model.Recipient = "All" then
                    Cmd.SignalR.send model.Hub (Action.SendMessageToAll model.EntryText)
                else
                    Cmd.SignalR.send
                        model.Hub
                        (Action.SendMessageToUser(model.Username, model.Recipient, model.EntryText))

            { model with EntryText = String.Empty }, cmd
        | MessageEntryText t -> { model with EntryText = t }, Cmd.none
        | UsernameEntryText u -> { model with Username = u }, Cmd.none
        | RecipientChanged r -> { model with Recipient = r }, Cmd.none
        | Login ->
            let getAccessTokenAsync =
                async {
                    try
                        let authUri = Uri($"{serverUrl}/auth")
                            
                        let! authResult =
                            WebAuthenticator.AuthenticateAsync(authUri, Uri("fabulouschat://"))
                            |> Async.AwaitTask
                            
                        let idToken = authResult.Properties.["idToken"]

                        return SetAuthResult idToken
                    with e ->
                        return LoginFailed e
                }

            model, Cmd.ofAsyncMsg getAccessTokenAsync
        | SetAuthResult jwtToken ->
            let username =
                let parsed = JwtSecurityTokenHandler().ReadJwtToken(jwtToken)
                parsed.Payload.Item("nickname").ToString()
            { model with IdToken = jwtToken; Username = username }, Cmd.ofMsg EnterChatRoom
        | LoginFailed e ->
            let message =
                let rec innermost (ex: exn) =
                    if ex.InnerException <> null
                    then innermost ex.InnerException
                    else ex.Message
                innermost e
            
            let display = async {
                do! Application.Current.MainPage.DisplayAlert("Login Failed", message, "Close") |> Async.AwaitTask
                return None
            }

            model, Cmd.ofAsyncMsgOption display
        | EnterChatRoom ->
            let cmd =
                Cmd.SignalR.connect
                    RegisterHub
                    (fun hub ->
                        hub
                            .WithUrl($"{serverUrl}{Shared.Endpoints.Root}", fun opt ->
#if DEBUG
                                opt.HttpMessageHandlerFactory <- fun msg ->
                                    match msg with
                                    | :? HttpClientHandler as clientHandler ->
                                        clientHandler.ServerCertificateCustomValidationCallback <- fun _ _ _ _ -> true
                                        clientHandler :> HttpMessageHandler
                                    | _ -> msg
#endif
                                if not <| String.IsNullOrWhiteSpace(model.IdToken)
                                then opt.AccessTokenProvider <- (fun () -> Task.FromResult(model.IdToken))
                                )
                            .WithAutomaticReconnect()
                            .ConfigureLogging(fun logBuilder -> logBuilder.SetMinimumLevel(LogLevel.Debug))
                            .OnMessage SignalRMessage)

            model, cmd
        | NoOp -> model, Cmd.none

    let chatMessages (model: Model) =
        let messages =
            model.Messages
            |> List.map
                (fun m ->
                    let color =
                        if m.EndsWith("connected") then
                            Color.Gray
                        else
                            Color.Black

                    View.Label(text = m, textColor = color))

        View.ScrollView(View.StackLayout(children = messages))

    let usernameEntry (model: Model) dispatch =
        View.StackLayout(
            padding = Thickness 5.0,
            children =
                [ yield View.Entry(
                    placeholder = "Enter your username",
                    text = model.Username,
                    textChanged = (fun args -> dispatch (UsernameEntryText args.NewTextValue))
                  )
                  yield View.Button(text = "Enter chat room", command = (fun () -> dispatch EnterChatRoom))
                  if Device.RuntimePlatform = Device.Android then yield View.Button(text = "Login", command = (fun () -> dispatch Login))  ]
        )

    let chatRoom (model: Model) dispatch =
        View.Grid(
            columnSpacing = 2.0,
            rowSpacing = 2.0,
            rowdefs = [ Stars 10.0; Stars 1.0 ],
            coldefs = [ Star; Star; Star; Star ],
            padding = Thickness 5.0,
            children =
                [ (chatMessages model).ColumnSpan(3)
                  View
                      .Entry(placeholder = "Enter chat message",
                             text = model.EntryText,
                             textChanged = (fun args -> dispatch (MessageEntryText args.NewTextValue)))
                      .Row(1)
                      .ColumnSpan(2)
                  View
                      .Picker(items = [ "All"; yield! model.Participants ],
                              selectedIndexChanged = (fun (i, item) ->
                                  if item.IsSome then
                                      dispatch (RecipientChanged item.Value)))
                      .Row(1)
                      .Column(2)
                  View
                      .Button(text = "Send", command = (fun () -> dispatch SendMessage))
                      .Row(1)
                      .Column(3) ]
        )

    let view (model: Model) dispatch =
        View.ContentPage(
            content =
                (if model.Hub.IsSome then
                     chatRoom
                 else
                     usernameEntry)
                    model
                    dispatch
        )

    // Note, this declaration is needed if you enable LiveUpdate
    let program = Program.mkProgram init update view

type App() as app =
    inherit Application()

    let runner =
        App.program
#if DEBUG
        |> Program.withConsoleTrace
#endif
        |> XamarinFormsProgram.run app
