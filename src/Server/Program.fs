namespace Server

open System

open Giraffe
open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Hosting

module Server =
    let webApp : HttpHandler =
        Remoting.createApi()
        |> Remoting.fromValue RemoteApi.shoppingApi
        |> Remoting.withRouteBuilder Shared.Route.builder
        |> Remoting.buildHttpHandler

    let configureApp (app : IApplicationBuilder) =
        // Add Giraffe to the ASP.NET Core pipeline
        app.UseGiraffe webApp

    let configureServices (services : IServiceCollection) =
        // Add Giraffe dependencies
        services.AddGiraffe() |> ignore

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