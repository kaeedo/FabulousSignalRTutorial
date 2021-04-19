namespace Server

open System
open System.Net
open System.Security.Claims
open System.Threading.Tasks
open Giraffe
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Hosting
open System.Collections.Generic
open Fable.SignalR
open FSharp.Control.Tasks.V2
open Shared.SignalRHub

module ChatHub =
    let invoke (msg: Action) (hubContext: FableHub) =
        task { return Response.ParticipantConnected String.Empty }

    let send (msg: Action) (hubContext: FableHub<Action, Response>) =
        let participants =
            hubContext.Services.GetService<HashSet<string>>()

        match msg with
        | Action.ClientConnected participant ->
            task {
                do! hubContext.Groups.AddToGroupAsync(hubContext.Context.ConnectionId, participant)

                let tasks =
                    participants
                    |> Seq.map
                        (fun p ->
                            hubContext
                                .Clients
                                .Group(p)
                                .Send(Response.ParticipantConnected participant))

                do participants.Add(participant) |> ignore

                return! Task.WhenAll(tasks)
            }
            :> Task
        | Action.SendMessageToAll message ->
            participants
            |> Seq.map
                (fun p ->
                    hubContext
                        .Clients
                        .Group(p)
                        .Send(Response.ReceiveMessage message))
            |> fun tasks -> Task.WhenAll(tasks)
        | Action.SendMessageToUser (sender, recipient, message) ->
            hubContext
                .Clients
                .Group(recipient)
                .Send(Response.ReceiveDirectMessage(sender, message))

    let config =
        { SignalR.Settings.EndpointPattern = Shared.Endpoints.Root
          SignalR.Settings.Send = send
          SignalR.Settings.Invoke = invoke
          SignalR.Settings.Config = None }

module Server =
    let authenticate : HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let scheme = "GitHub"
                let! auth = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)

                if ((not auth.Succeeded)
                    || auth.Principal = null
                    || auth.Principal.Identities
                       |> Seq.exists (fun i -> i.IsAuthenticated)
                       |> not
                    || String.IsNullOrWhiteSpace(auth.Properties.GetTokenValue("access_token"))) then

                    do! ctx.ChallengeAsync(scheme)
                    return! next ctx
                else
                    let claims = auth.Principal.Identities |> Seq.tryHead

                    let username =
                        claims
                        |> Option.map (fun c -> c.Name)
                        |> Option.defaultValue String.Empty

                    let expires =
                        auth.Properties.ExpiresUtc
                        |> Option.ofNullable
                        |> Option.map (fun utc -> utc.ToUnixTimeSeconds().ToString())
                        |> Option.defaultValue "-1"

                    let qs =
                        [ "access_token", auth.Properties.GetTokenValue("access_token")
                          "refresh_token",
                          if String.IsNullOrWhiteSpace(auth.Properties.GetTokenValue("refresh_token")) then
                              String.Empty
                          else
                              auth.Properties.GetTokenValue("refresh_token")
                          "expire", expires
                          "username", username ]
                        |> dict

                    let url =
                        "fabulouschat://#"
                        + String.Join(
                            "&",
                            qs
                            |> Seq.filter
                                (fun kvp ->
                                    (String.IsNullOrWhiteSpace(kvp.Value) |> not)
                                    && kvp.Value <> "-1")
                            |> Seq.map (fun kvp -> $"{WebUtility.UrlEncode(kvp.Key)}={WebUtility.UrlEncode(kvp.Value)}")
                        )

                    return! redirectTo false url next ctx
            }

    let webApp : HttpHandler =
        choose [ GET >=> route "/" >=> text "hello"
                 GET >=> route "/auth" >=> authenticate ] //requiresAuthentication (challenge "GitHub") >=> redirectTo false "/fabulouschat" //fun (next : HttpFunc) (ctx : HttpContext) -> printfn "authing"; requiresAuthentication (challenge "GitHub") next ctx

    let configureApp (app: IApplicationBuilder) =
        //app.UseAuthentication() |> ignore
        //app.UseAuthorization() |> ignore
        app.UseSignalR(ChatHub.config) |> ignore
        app.UseGiraffe webApp

    let configureAppConfiguration (context: WebHostBuilderContext) (config: IConfigurationBuilder) =
        config.AddUserSecrets(Reflection.Assembly.GetCallingAssembly())
        |> ignore

    let configureServices (services: IServiceCollection) =
        let sp = services.BuildServiceProvider()
        let conf = sp.GetService<IConfiguration>()
        services.AddSingleton<HashSet<string>>() |> ignore

        services
            .AddAuthentication(fun options ->
                options.DefaultScheme <- CookieAuthenticationDefaults.AuthenticationScheme
                options.DefaultChallengeScheme <- "GitHub")
            .AddCookie()
            .AddGitHub(fun options ->
                options.ClientId <- conf.["GithubClientId"]
                options.ClientSecret <- conf.["GithubClientSecret"]
                //options.CallbackPath <- PathString("/auth2")

                //options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id")
                //options.ClaimActions.MapJsonKey(ClaimTypes.Name, "name")

                //options.AuthorizationEndpoint <- "https://github.com/login/oauth/authorize"
                //options.TokenEndpoint <- "https://github.com/login/oauth/access_token"
                //options.UserInformationEndpoint <- "https://api.github.com/user"
                options.SaveTokens <- true)
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
            .Build()
            .Run()

        0
