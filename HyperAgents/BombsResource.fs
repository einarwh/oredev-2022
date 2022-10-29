module BombsResource

open Utils
open Siren
open HttpUtils
open Microsoft.AspNetCore.Http

type RequestInfo = HttpContext
type ResponseInfo =
      | Success of SirenDocument
      | Failure

type BombId = int
type Url = string

type Message = 
  | Lookup of BombId * AsyncReplyChannel<Agent<BombResource.Message> option>
  | Register of Agent<BombResource.Message> * AsyncReplyChannel<BombId>

let agentRef = Agent<Message>.Start (fun inbox ->
  let rec loop (bombs : Map<BombId, Agent<BombResource.Message>>) = async {
    let! msg = inbox.Receive()
    match msg with 
    | Lookup (bombId, replyChannel) ->
      bombs.TryFind bombId |> replyChannel.Reply
      return! loop bombs
    | Register (bomb, replyChannel) ->
      let bombId = bombs.Count
      printfn "Register new bomb with id %d" bombId
      bombId |> replyChannel.Reply 
      return! loop <| bombs.Add(bombId, bomb)
  }
  loop Map.empty
)
