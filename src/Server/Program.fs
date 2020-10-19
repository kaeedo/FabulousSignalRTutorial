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


        // xml/network_security_config.xml
//<?xml version="1.0" encoding="utf-8"?>
//<network-security-config>
//    <base-config cleartextTrafficPermitted="true" />
//</network-security-config>


// properties/androimanifest
//<?xml version="1.0" encoding="utf-8"?>
//<manifest xmlns:android="http://schemas.android.com/apk/res/android" 
//          android:versionCode="1" 
//          android:versionName="1.0" 
//          package="dev.hashset.chat.fabulous.droid">
//  <uses-sdk android:minSdkVersion="26" android:targetSdkVersion="28" />
//  <application 
//      android:allowBackup="true" 
//      android:icon="@mipmap/ic_launcher" 
//      android:label="@string/app_name" 
//      android:roundIcon="@mipmap/ic_launcher_round" 
//      android:supportsRtl="true" 
//      android:theme="@style/AppTheme"
//      android:networkSecurityConfig="@xml/network_security_config">
//  </application>
//  <uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />
//</manifest>
