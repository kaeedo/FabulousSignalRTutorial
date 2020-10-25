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
    let update (msg: Action) =
        match msg with 
        | Action.ClientConnected participant -> Response.ParticipantConnected [participant]
        | Action.SendMessageToAll message -> Response.ReceiveMessage message
        | Action.SendMessageToUser (sender, message) -> Response.ReceiveDirectMessage (sender, message)

    let invoke (msg: Action) (hubContext: FableHub) =
        task {
            return update msg
        }

    let send (msg: Action) (hubContext: FableHub<Action,Response>) =
        update msg
        |> hubContext.Clients.All.Send

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