namespace Server

open System
open System.Net
open System.Security.Claims
open System.Threading.Tasks
open Giraffe
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Authentication.OpenIdConnect
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Hosting
open System.Collections.Generic
open Fable.SignalR
open FSharp.Control.Tasks.V2
open Microsoft.Extensions.Logging
open Microsoft.IdentityModel.Protocols.OpenIdConnect
open Shared.SignalRHub

open Auth

module ChatHub =
    let invoke (msg: Action) (hubContext: FableHub) =
        task { return Response.ParticipantConnected(false, String.Empty) }

    let send (msg: Action) (hubContext: FableHub<Action, Response>) =
        let guests =
            hubContext.Services.GetService<HashSet<string>>()

        let authenticatedUsers =
            hubContext.Services.GetService<Dictionary<string, string>>()

        match msg with
        | Action.ClientConnected participant ->
            task {
                let participant =
                    if hubContext.Context.User.Identity.IsAuthenticated then
                        (hubContext.Context.User.Claims |> Seq.find (fun c -> c.Type = "nickname")).Value
                    else
                        participant

                let tasks =
                    seq {
                        yield! guests |> Seq.map (fun g -> hubContext.Clients.Caller.Send(Response.ParticipantConnected(false, g)))
                        yield! authenticatedUsers |> Seq.map (fun kvp -> hubContext.Clients.Caller.Send(Response.ParticipantConnected(true, kvp.Value)))
                        hubContext.Clients.Others.Send(Response.ParticipantConnected(hubContext.Context.User.Identity.IsAuthenticated, participant))
                    }
                    
                if hubContext.Context.User.Identity.IsAuthenticated then
                    let userId = hubContext.Context.UserIdentifier
                    do authenticatedUsers.Add(userId, participant)
                else
                    do guests.Add(participant) |> ignore
                    do! hubContext.Groups.AddToGroupAsync(hubContext.Context.ConnectionId, participant)

                return! Task.WhenAll(tasks)
            }
            :> Task
        | Action.SendMessageToAll message -> hubContext.Clients.All.Send(Response.ReceiveMessage message)
        | Action.SendMessageToUser (sender, recipient, message) ->
            if hubContext.Context.User.Identity.IsAuthenticated then
                if guests.Contains(recipient) then
                    hubContext
                        .Clients
                        .Group(recipient)
                        .Send(Response.ReceiveDirectMessage(sender, message))
                else
                    let userId = (authenticatedUsers |> Seq.find (fun kvp -> kvp.Value = recipient)).Key
                    hubContext.Clients.User(userId).Send(Response.ReceiveDirectMessage(sender, message))
            else
                hubContext.Clients.Caller.Send(Response.Unauthorized "Only logged in users may send direct messages")

    let config =
        SignalR
            .ConfigBuilder(Shared.Endpoints.Root, send, invoke)
            .LogLevel(LogLevel.Debug)
            .AfterUseRouting(fun app -> app.UseAuthorization())
            .EnableBearerAuth()
            .Build()

module Server =
    let secured : HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let user =
                    ctx.User.Claims
                    |> Seq.map (fun (i: Claim) -> {| Type = i.Type; Value = i.Value |})

                let! idToken = ctx.GetTokenAsync("id_token")

                return! json {| User = user; IdToken = idToken |} next ctx
            }

    let authenticate : HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let! auth = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)

                if ((not auth.Succeeded)
                    || auth.Principal = null
                    || auth.Principal.Identities
                       |> Seq.exists (fun i -> i.IsAuthenticated)
                       |> not) then
                    // https://auth0.com/docs/quickstart/webapp/aspnet-core/01-login
                    do! ctx.ChallengeAsync("Auth0") // This scheme name maps to the scheme name registered in the AddOpenIDConnect middleware below
                    return! next ctx
                else
                    let! idToken = ctx.GetTokenAsync(CookieAuthenticationDefaults.AuthenticationScheme, "id_token")

                    let url =
                        $"fabulouschat://#idToken={WebUtility.UrlEncode(idToken)}"

                    return! redirectTo false url next ctx
            }


    let webApp : HttpHandler =
        choose [ GET >=> route "/" >=> text "hello"
                 GET >=> route "/callback" >=> text "callback"
                 GET >=> route "/auth" >=> authenticate ]

    let configureApp (app: IApplicationBuilder) =
        app.UseDeveloperExceptionPage() |> ignore
        app.UseAuthentication() |> ignore
        app.UseAuthorization() |> ignore
        app.UseSignalR(ChatHub.config) |> ignore
        app.UseGiraffe webApp

    let configureAppConfiguration (context: WebHostBuilderContext) (config: IConfigurationBuilder) =
        config.AddUserSecrets(Reflection.Assembly.GetCallingAssembly())
        |> ignore

    let configureLogging (builder: ILoggingBuilder) =
        let filter (l: LogLevel) = l.Equals LogLevel.Debug

        builder.AddFilter(filter).AddConsole().AddDebug()
        |> ignore

    let configureServices (services: IServiceCollection) =
        let sp = services.BuildServiceProvider()
        let conf = sp.GetService<IConfiguration>()
        services.AddSingleton<HashSet<string>>() |> ignore

        services.AddSingleton<Dictionary<string, string>>()
        |> ignore

        services
            .AddAuthentication(fun opts ->
                opts.DefaultScheme <- JwtBearerDefaults.AuthenticationScheme

                opts.DefaultSignInScheme <- CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie()
            .AddOpenIdConnect("Auth0",
                              fun opts ->
                                  opts.Authority <- "https://fabulous-tutorial.eu.auth0.com"
                                  opts.ClientId <- conf.["Auth0ClientId"]
                                  opts.ClientSecret <- conf.["Auth0ClientSecret"]
                                  opts.SaveTokens <- true

                                  opts.ResponseType <- OpenIdConnectResponseType.Code

                                  opts.CallbackPath <- PathString("/callback")

                                  opts.ClaimsIssuer <- "Auth0"

                                  opts.Events <-
                                      OpenIdConnectEvents(
                                          OnRedirectToIdentityProvider =
                                              fun ctx ->
                                                  ctx.ProtocolMessage.SetParameter("audience", "https://fabulous-chat/")
                                                  // This audience is from API in Auth0

                                                  Task.CompletedTask
                                      )

            )
            .AddJwtBearer(fun opts ->
                opts.SaveToken <- true

                opts.IncludeErrorDetails <- true
                opts.Authority <- "https://fabulous-tutorial.eu.auth0.com/"
                opts.Audience <- conf.["Auth0ClientId"] 
                // The audience is the Auth0 Application client ID since that is who issued the JWT token via the OIDC middleware above
                )
        |> ignore

        services.AddGiraffe() |> ignore
        services.AddSignalR(ChatHub.config) |> ignore


    [<EntryPoint>]
    let main _ =
        WebHostBuilder()
            .UseKestrel()
            .UseUrls("http://0.0.0.0:5000", "https://0.0.0.0:5001")
            .ConfigureAppConfiguration(configureAppConfiguration)
            .Configure(Action<IApplicationBuilder> configureApp)
            .ConfigureServices(configureServices)
            .ConfigureLogging(configureLogging)
            .Build()
            .Run()

        0
