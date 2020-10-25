namespace Server

open System
open Giraffe
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Hosting
open System.Collections.Generic
open Fable.SignalR
open FSharp.Control.Tasks.V2
open Shared.SignalRHub

module ChatHub =
    let invoke (msg: Action) (hubContext: FableHub) =
        task {
            return Response.ParticipantConnected [String.Empty]
        }

    let send (msg: Action) (hubContext: FableHub<Action, Response>) =
        let participants = hubContext.Services.GetService<Dictionary<string, string>>()

        match msg with
        | Action.ClientConnected participant -> 
            participants.[participant] <- hubContext.Context.ConnectionId
            Response.ParticipantConnected (participants.Keys |> List.ofSeq)
            |> hubContext.Clients.All.Send
        | Action.SendMessageToAll message -> 
            Response.ReceiveMessage message
            |> hubContext.Clients.All.Send
        | Action.SendMessageToUser (recipient, message) -> 
            let sender = 
                participants.Keys
                |> List.ofSeq
                |> List.find (fun k -> 
                    participants.[k] = hubContext.Context.ConnectionId
                )

            let recipientConnectionId = participants.[recipient]
            Response.ReceiveDirectMessage (sender, message)
            |> hubContext.Clients.Client(recipientConnectionId).Send

    let config =
        { SignalR.Settings.EndpointPattern = Shared.Endpoints.Root
          SignalR.Settings.Send = send
          SignalR.Settings.Invoke = invoke 
          SignalR.Settings.Config = None }
    
module Server =
    let webApp : HttpHandler =
        choose [
            GET >=> route "/" >=> text "hello"
        ]

    let configureApp (app : IApplicationBuilder) =
        app.UseSignalR(ChatHub.config) |> ignore
        app.UseGiraffe webApp

    let configureServices (services : IServiceCollection) =
        services.AddSingleton<Dictionary<string, string>>() |> ignore

        services.AddGiraffe() |> ignore
        services.AddSignalR(ChatHub.config) |> ignore

    [<EntryPoint>]
    let main _ =
        WebHostBuilder()
            .UseKestrel()
            .UseUrls("http://0.0.0.0:5000", "https://0.0.0.0:5001")
            .Configure(Action<IApplicationBuilder> configureApp)
            .ConfigureServices(configureServices)
            .Build()
            .Run()
        0