namespace Server

open System
open System.Linq
open Giraffe
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.SignalR
open System.Collections.Generic

type ChatHub(connectedUsers: Dictionary<string, string>) =
    inherit Hub()

    member __.SendMessageToAll(message: string) =
        printfn "Sending \"%s\" to all connected clients" message
        __.Clients.All.SendAsync("ReceiveMessage", message) |> Async.AwaitTask |> Async.RunSynchronously

    member __.SendMessageToUser(user: string, message: string) =
        printfn "Sending \"%s\" to \"%s\"" message user
        let caller = 
            connectedUsers.Keys.Where(fun k -> connectedUsers.[k] = __.Context.ConnectionId).First()

        let clientId = connectedUsers.[user]
        __.Clients.Client(clientId).SendAsync("ReceiveDirectMessage", caller, message) |> Async.AwaitTask |> Async.RunSynchronously

    member __.ClientConnected(username: string) =
        printfn "%s connected" username
        connectedUsers.[username] <- __.Context.ConnectionId

        let allParticipants = connectedUsers.Keys.ToList()

        __.Clients.All.SendAsync("ParticipantConnected", allParticipants)  |> Async.AwaitTask |> Async.RunSynchronously


module Server =
    let webApp : HttpHandler =
        choose [
            GET >=> route "/" >=> text "hello"
        ]

    let configureApp (app : IApplicationBuilder) =
        // Needed together with endpoints
        app.UseRouting() |> ignore
        app.UseEndpoints(fun endpoints ->
            endpoints.MapHub<ChatHub>("/chathub") |> ignore
        ) |> ignore

        app.UseGiraffe webApp

    let configureServices (services : IServiceCollection) =
        services.AddSingleton<Dictionary<string, string>>() |> ignore

        services.AddGiraffe() |> ignore
        services.AddSignalR() |> ignore

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