// Copyright 2018-2019 Fabulous contributors. See LICENSE.md for license.
namespace ShoppingList

open Fabulous
open Fabulous.XamarinForms
open Xamarin.Forms
open Fable.Remoting.DotnetClient
open Shared

module App = 
    type Model = 
      { Items: string list }

    type Msg = 
        | GetItems
        | SetItems of ShoppingItem list
        | Reset

    let shoppingApi =
        // must be HTTPS because android security stuff. 
        // localhost is the device itself

        //https://nicksnettravels.builttoroam.com/android-certificates/
        let routes = sprintf "http://10.193.16.165:5000/api/%s/%s"
        let proxy = Proxy.create<IShoppingApi> routes 

        proxy

    let initModel = { Items = [] }

    let init () = initModel, Cmd.none

    let getShoppingCmd =
        async {
            let! items = shoppingApi.call <@ fun api -> api.GetShoppingList () @>

            return SetItems items
        }
        |> Cmd.ofAsyncMsg

    let update msg model =
        match msg with
        | GetItems -> model, getShoppingCmd
        | SetItems items -> { model with Items = items |> List.map (fun i -> i.Name) }, Cmd.none
        | Reset -> init ()

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