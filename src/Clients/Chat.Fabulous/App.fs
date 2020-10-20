namespace Chat.Fabulous

open System
open Fabulous
open Fabulous.XamarinForms
open Xamarin.Forms
open Microsoft.AspNetCore.SignalR.Client

module App = 
    type Model = 
      { Messages: string list
        EntryText: string
        ChatHub: HubConnection }

    type Msg = 
        | EntryText of string
        | SendMessage
        | ReceivedMessage of string
        | NoOp

    // https://montemagno.com/real-time-communication-for-mobile-with-signalr/
    // https://nicksnettravels.builttoroam.com/android-certificates/
    let initModel = 
        let hub = 
            HubConnectionBuilder()
                .WithUrl("http://192.168.1.103:5000/chathub")
                .WithAutomaticReconnect()
                .Build()

        hub.StartAsync() |> Async.AwaitTask |> ignore

        { Messages = []; EntryText = String.Empty; ChatHub = hub }

    let setupListeners (dispatch: Msg -> unit) (hub: HubConnection) =
        hub.On<string>("ReceiveMessage", fun message ->
            dispatch <| ReceivedMessage message
        ) |> ignore

    let init () = 
        initModel, Cmd.ofSub (fun (dispatch: Msg -> unit) -> setupListeners dispatch initModel.ChatHub)

    let sendMessageCmd (model: Model) =
        async {
            do! model.ChatHub.InvokeAsync("SendMessageToAll", model.EntryText) |> Async.AwaitTask
            return NoOp
        }
        |> Cmd.ofAsyncMsg

    let update msg model =
        match msg with
        | SendMessage -> { model with EntryText = String.Empty }, (sendMessageCmd model)
        | ReceivedMessage m -> { model with Messages = m :: model.Messages }, Cmd.none
        | EntryText t -> { model with EntryText = t }, Cmd.none
        | NoOp -> model, Cmd.none

    let chatMessages (model: Model) =
        let messages =
            model.Messages
            |> List.map (fun m -> View.Label(text = m))
        View.ScrollView(View.StackLayout(children = messages))

    let view (model: Model) dispatch =
        View.ContentPage(
          content = View.Grid(
            columnSpacing = 2.0,
            rowSpacing = 2.0,
            rowdefs = [Stars 10.0; Stars 1.0],
            coldefs = [Star; Star; Star],
            padding = Thickness 5.0,
            children = [ 
                (chatMessages model).ColumnSpan(3)
                View.Entry(
                    placeholder = "Enter chat message",
                    text = model.EntryText,
                    textChanged = (fun args -> dispatch (EntryText args.NewTextValue))
                ).Row(1).ColumnSpan(2)
                View.Button(
                    text = "Send", 
                    command = (fun () -> dispatch SendMessage)
                ).Row(1).Column(2)
            ]))

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