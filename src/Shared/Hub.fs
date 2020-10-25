namespace Shared

module SignalRHub =
    [<RequireQualifiedAccess>]
    type Action =
    | SendMessageToAll of string
    | SendMessageToUser of string * string
    | ClientConnected of string

    [<RequireQualifiedAccess>]
    type Response =
    | ReceiveMessage of string
    | ReceiveDirectMessage of string * string
    | ParticipantConnected of string list

[<RequireQualifiedAccess>]
module Endpoints =
    let [<Literal>] Root = "/chathub"