namespace Server

open Shared

module RemoteApi =
    let mutable items =
        [| { ShoppingItem.Name = "Milk"
             Quantity = 1 }
           { ShoppingItem.Name = "Chocolate"
             Quantity = 4 } |]

    let getShoppingList () = async { return items |> Array.toList }

    let updateItem itemToUpdate =
        async {
            let item =
                items
                |> Array.tryFindIndex (fun i -> i.Name = itemToUpdate.Name)

            match item with
            | Some i ->
                items.[i] <- itemToUpdate
                return true
            | None -> return false

        }

    let addItem itemToAdd =
        async {
            let newItems = itemToAdd :: (items |> Array.toList)
            items <- newItems |> List.toArray

            return true
        }

    let shoppingApi: IShoppingApi =
        { AddItem = addItem
          UpdateItem = updateItem
          GetShoppingList = getShoppingList }
