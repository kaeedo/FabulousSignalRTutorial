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
        Username: string
        Participants: string list
        Recipient: string
        ChatHub: HubConnection option }

    type Msg = 
        | MessageEntryText of string
        | SendMessage
        | ReceivedMessage of string
        | UsernameEntryText of string
        | EnterChatRoom
        | RecipientChanged of string
        | ParticipantConnected of string list
        | NoOp

    // https://montemagno.com/real-time-communication-for-mobile-with-signalr/
    // https://nicksnettravels.builttoroam.com/android-certificates/
    let initModel = 
        { Messages = []; EntryText = String.Empty; ChatHub = None; Username = String.Empty; Participants = []; Recipient = String.Empty }

    let setupListeners (dispatch: Msg -> unit) (hub: HubConnection) =
        hub.On<string>("ReceiveMessage", fun message ->
            dispatch <| ReceivedMessage message
        ) |> ignore

        hub.On<string, string>("ReceiveDirectMessage", fun sender message ->
            dispatch <| ReceivedMessage (sprintf "%s whispered: %s" sender message)
        ) |> ignore

        hub.On<System.Collections.Generic.List<string>>("ParticipantConnected", fun participants ->
            let participants = participants |> List.ofSeq
            dispatch <| ParticipantConnected participants
        ) |> ignore

    let init () = 
        initModel, Cmd.none

    let sendMessageCmd (model: Model) =
        let task =
            if String.IsNullOrWhiteSpace(model.Recipient) || model.Recipient = "All"
            then model.ChatHub.Value.InvokeAsync("SendMessageToAll", model.EntryText)
            else model.ChatHub.Value.InvokeAsync("SendMessageToUser", model.Recipient, model.EntryText)
        async {
            do! task |> Async.AwaitTask
            return NoOp
        }
        |> Cmd.ofAsyncMsg
    
    let setConnectedCmd (hub: HubConnection) (username: string) =
        async {
            do! hub.InvokeAsync("ClientConnected", username) |> Async.AwaitTask
            return NoOp
        }
        |> Cmd.ofAsyncMsg

    let update msg model =
        match msg with
        | SendMessage -> { model with EntryText = String.Empty }, (sendMessageCmd model)
        | ReceivedMessage m -> { model with Messages = m :: model.Messages }, Cmd.none
        | MessageEntryText t -> { model with EntryText = t }, Cmd.none
        | UsernameEntryText u -> { model with Username = u }, Cmd.none
        | RecipientChanged r -> { model with Recipient = r }, Cmd.none
        | ParticipantConnected p -> 
            let userConnectedMessage = 
                sprintf "%s connected" (p |> List.last)
            { model with 
                Messages = userConnectedMessage :: model.Messages
                Participants = p }, Cmd.none
        | EnterChatRoom -> 
            let hub = 
                HubConnectionBuilder()
                    .WithUrl("http://192.168.1.103:5000/chathub")
                    .WithAutomaticReconnect()
                    .Build()

            hub.StartAsync() |> ignore

            let cmds = 
                Cmd.batch [
                    Cmd.ofSub (fun (dispatch: Msg -> unit) -> setupListeners dispatch hub)
                    setConnectedCmd hub model.Username
                ]

            { model with ChatHub = Some hub }, cmds
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
        View.ContentPage(content = (if model.ChatHub.IsSome then chatRoom else usernameEntry) model dispatch)

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