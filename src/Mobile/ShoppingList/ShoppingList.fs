// Copyright 2018-2019 Fabulous contributors. See LICENSE.md for license.
namespace ShoppingList

open Fabulous
open Fabulous.XamarinForms
open Xamarin.Forms
open Fable.Remoting.DotnetClient
open Shared
open Microsoft.AspNetCore.SignalR.Client

module App = 
    type Model = 
      { Items: string list
        Hub: HubConnection }

    type Msg = 
        | GetItems
        | SetItems of ShoppingItem list
        | SetItem of string * bool
        | Reset
        | NoOp

    let shoppingApi =
        // must be HTTPS because android security stuff. 
        // localhost is the device itself

        // https://montemagno.com/real-time-communication-for-mobile-with-signalr/
        //https://nicksnettravels.builttoroam.com/android-certificates/
        let routes = sprintf "http://10.193.16.165:5000/api/%s/%s"
        let proxy = Proxy.create<IShoppingApi> routes 

        proxy

    let initModel = 
        let hub = 
            HubConnectionBuilder()
                .WithUrl("http://10.193.16.165:5000/shoppinglisthub")
                .WithAutomaticReconnect()
                .Build()

        hub.StartAsync() |> Async.AwaitTask |> ignore

        { Items = []; Hub = hub }

    let setupListeners (dispatch: Msg -> unit) (hub: HubConnection) =
        hub.On<string, bool>("UpsertItem", fun item isDone ->
            // let finalMessage = sprintf "%s says %b" item isDone
            dispatch <| SetItem (item, isDone)
        ) |> ignore

    let init () = 
        initModel, Cmd.ofSub (fun (dispatch: Msg -> unit) -> setupListeners dispatch initModel.Hub)

    let getShoppingCmd =
        async {
            let! items = shoppingApi.call <@ fun api -> api.GetShoppingList () @>

            return SetItems items
        }
        |> Cmd.ofAsyncMsg

    let upsertShoppingCmd (hub: HubConnection) =
        async {
            do! hub.InvokeAsync("UpsertItem", "New Item", false) |> Async.AwaitTask
            return NoOp
        }
        |> Cmd.ofAsyncMsg

    let update msg model =
        match msg with
        | GetItems -> model, upsertShoppingCmd model.Hub // getShoppingCmd
        | SetItems items -> { model with Items = items |> List.map (fun i -> i.Name) }, Cmd.none
        | SetItem (item, _) -> { model with Items = item :: model.Items }, Cmd.none
        | Reset -> init ()
        | NoOp -> model, Cmd.none

    let items model = 
        model.Items 
        |> List.map (fun i -> View.Label(text=i))

    let view (model: Model) dispatch =
        View.ContentPage(
          content = View.StackLayout(padding = Thickness 20.0, verticalOptions = LayoutOptions.Center,
            children = [ 
                yield! items model
                View.Button(text = "Get shopping items", command = (fun () -> dispatch GetItems), horizontalOptions = LayoutOptions.Center)
                View.Button(text = "Reset", horizontalOptions = LayoutOptions.Center, command = (fun () -> dispatch Reset), commandCanExecute = (model <> initModel))
            ]))

    // Note, this declaration is needed if you enable LiveUpdate
    let program = XamarinFormsProgram.mkProgram init update view

type App () as app = 
    inherit Application ()

    let runner = 
        App.program
#if DEBUG
        |> Program.withConsoleTrace
#endif
        |> XamarinFormsProgram.run app