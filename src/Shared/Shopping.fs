namespace Shared

type ShoppingItem =
    { Name: string
      Quantity: int }

type IShoppingApi =
    { AddItem: ShoppingItem -> Async<bool>
      UpdateItem: ShoppingItem -> Async<bool>
      GetShoppingList: unit -> Async<ShoppingItem list> }

module Route =
    /// Defines how routes are generated on server and mapped from client
    let builder typeName methodName =
        sprintf "/api/%s/%s" typeName methodName