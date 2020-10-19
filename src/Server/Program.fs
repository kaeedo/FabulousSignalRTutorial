namespace Server

open System

open Giraffe
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.SignalR

type ChatHub() =
    inherit Hub()
    member __.SendMessageToAll(message: string) =
        printfn "Received: %s" message
        __.Clients.All.SendAsync("ReceiveMessage", message)

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