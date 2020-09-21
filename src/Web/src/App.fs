module App

open Browser.Dom
open Fable.Remoting.Client
open Shared

// studentApi : IStudentApi
let shoppingApi =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.buildProxy<IShoppingApi>

async {
  // students : Student[]
  let! shoppingItems = shoppingApi.GetShoppingList()
  for item in shoppingItems do
    // student : Student
    printfn "Item: %s quantity: %d" item.Name item.Quantity
}
|> Async.StartImmediate

// Mutable variable to count the number of times we clicked the button
let mutable count = 0

// Get a reference to our button and cast the Element to an HTMLButtonElement
let myButton = document.querySelector(".my-button") :?> Browser.Types.HTMLButtonElement

// Register our listener
myButton.onclick <- fun _ ->
    count <- count + 1
    myButton.innerText <- sprintf "You clicked: %i time(s)" count
