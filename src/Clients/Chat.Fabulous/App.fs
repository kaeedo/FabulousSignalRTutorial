namespace Chat.Fabulous

open System
open Fabulous
open Fabulous.XamarinForms
open Xamarin.Forms
open Microsoft.AspNetCore.SignalR.Client
open Fable.SignalR.Elmish
open Shared.SignalRHub

module App = 
    type Model = 
      { Messages: string list
        EntryText: string
        Username: string
        Participants: string list
        Recipient: string
        Hub: Elmish.Hub<Action, Response> option
        ChatHub: HubConnection option }

    type Msg = 
        | MessageEntryText of string
        | SendMessage
        | UsernameEntryText of string
        | EnterChatRoom
        | RecipientChanged of string
        | NoOp
        | RegisterHub of Elmish.Hub<Action,Response>
        | SignalRMessage of Response

    // https://montemagno.com/real-time-communication-for-mobile-with-signalr/
    // https://nicksnettravels.builttoroam.com/android-certificates/
    let initModel = 
        { Messages = []
          EntryText = String.Empty
          Hub = None
          ChatHub = None
          Username = String.Empty
          Participants = []
          Recipient = String.Empty }

    let init () = 
        initModel, Cmd.none

    let update msg model =
        match msg with
        | RegisterHub hub -> 
            let cmd = Cmd.SignalR.send model.Hub (Action.ClientConnected model.Username)
            { model with Hub = Some hub }, cmd
        | SignalRMessage response ->
            match response with
            | Response.ParticipantConnected participants ->
                let userConnectedMessage = 
                    sprintf "%s connected" (participants |> List.last)
                { model with 
                    Messages = userConnectedMessage :: model.Messages
                    Participants = participants }, Cmd.none

            | Response.ReceiveDirectMessage (sender, message) ->
                let message = (sprintf "%s whispered: %s" sender message)
                { model with Messages = message :: model.Messages }, Cmd.none
            | Response.ReceiveMessage message ->
                { model with Messages = message :: model.Messages }, Cmd.none

        | SendMessage -> 
            let cmd = 
                if String.IsNullOrWhiteSpace(model.Recipient) || model.Recipient = "All"
                then Cmd.SignalR.send model.Hub (Action.SendMessageToAll model.EntryText)
                else Cmd.SignalR.send model.Hub (Action.SendMessageToUser (model.Recipient, model.EntryText))

            { model with EntryText = String.Empty }, cmd
        | MessageEntryText t -> { model with EntryText = t }, Cmd.none
        | UsernameEntryText u -> { model with Username = u }, Cmd.none
        | RecipientChanged r -> { model with Recipient = r }, Cmd.none
        | EnterChatRoom -> 
            let cmd =
                Cmd.SignalR.connect RegisterHub (fun hub ->
                    hub.WithUrl(sprintf "http://192.168.1.103:5000%s" Shared.Endpoints.Root)
                       .WithAutomaticReconnect()
                       .OnMessage SignalRMessage)

            model, cmd
        | NoOp -> model, Cmd.none

    let chatMessages (model: Model) =
        let messages =
            model.Messages
            |> List.map (fun m -> 
                let color = 
                    if m.EndsWith("connected")
                    then Color.Gray
                    else Color.Black
                View.Label(text = m, textColor = color)
            )
        View.ScrollView(View.StackLayout(children = messages))

    let usernameEntry (model: Model) dispatch =
        View.StackLayout(
            padding = Thickness 5.0,
            children = [
                View.Entry(
                    placeholder = "Enter your username",
                    text = model.Username,
                    textChanged = (fun args -> dispatch (UsernameEntryText args.NewTextValue))
                )
                View.Button(
                    text = "Enter chat room",
                    command = (fun () -> dispatch EnterChatRoom)
                )
            ]
        )

    let chatRoom (model: Model) dispatch =
        View.Grid(
            columnSpacing = 2.0,
            rowSpacing = 2.0,
            rowdefs = [Stars 10.0; Stars 1.0],
            coldefs = [Star; Star; Star; Star],
            padding = Thickness 5.0,
            children = [ 
                (chatMessages model).ColumnSpan(3)
                View.Entry(
                    placeholder = "Enter chat message",
                    text = model.EntryText,
                    textChanged = (fun args -> dispatch (MessageEntryText args.NewTextValue))
                ).Row(1).ColumnSpan(2)
                View.Picker(
                    items = ["All"; yield! model.Participants],
                    selectedIndexChanged = (fun (i, item) -> 
                        if item.IsSome then dispatch (RecipientChanged item.Value)
                    )
                ).Row(1).Column(2)
                View.Button(
                    text = "Send", 
                    command = (fun () -> dispatch SendMessage)
                ).Row(1).Column(3)
            ])

    let view (model: Model) dispatch =
        View.ContentPage(content = (if model.Hub.IsSome then chatRoom else usernameEntry) model dispatch)

    // Note, this declaration is needed if you enable LiveUpdate
    let program = Program.mkProgram init update view

type App () as app = 
    inherit Application ()

    let runner = 
        App.program
#if DEBUG
        |> Program.withConsoleTrace
#endif
        |> XamarinFormsProgram.run app