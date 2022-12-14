module ExitRoomResource

open Siren
open TrappableRoomResource
open Utils
open Giraffe
open HttpUtils
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Primitives 

type RequestInfo = HttpContext * AgentColor * Agent<AgentResource.Message>
type Message = RequestInfo * AsyncReplyChannel<HttpHandler>

let roomInfo : RoomResourceUtils.RoomInfo = 
  { name = "exit-room"
    properties = 
      { title = "The exit room." 
        description = "You're in the exit room. Do you have the secret files? Then you should get out of here!" }
    linkInfos = 
      [ ("control-room", ["control-room"; "entrance"]) ]
  }

let agentRef = Agent<Message>.Start (fun inbox ->

  let addPlaneLink (doc : SirenDocument) =
    let planeLink = { rel = [ "escape" ]; href = linkTo "plane"}
    let links' = doc.links @ [planeLink]
    { doc with links = links' }

  let trappableRoomAgent = createAgent roomInfo
  let rec loop() = async {
    let! ((ctx, clr, agent), replyChannel) = inbox.Receive()
    let! res = trappableRoomAgent.PostAndAsyncReply(fun ch -> ((ctx, clr, agent), ch))
    (* Ask secret file where it is. *)
    let! fileAt = SecretFileResource.agentRef.PostAndAsyncReply(fun ch -> SecretFileResource.LocationQuery ch)
    let planeAvailable = 
      match fileAt with
      | SecretFileResource.SecretFileLocation.TakenByAgent agentColor 
        when agentColor = clr ->
        true
      | SecretFileResource.SecretFileLocation.TakenByAgent agentColor ->
        printfn "D'oh the %s agent has the secret file." agentColor
        false
      | SecretFileResource.SecretFileLocation.RoomLocation roomLoc ->
        printfn "No one has the secret file - no one can leave."
        false
    let handler = 
      match res with 
      | Ok doc ->
        let doc' = if planeAvailable then addPlaneLink doc else doc
        Successful.OK doc' 
      | Created (doc, loc) ->
        Successful.CREATED doc >=> setHttpHeader "location" (loc |> uri2str)
      | Found loc ->
        redirectTo false (loc |> uri2str)
      | BadRequest why ->
        RequestErrors.BAD_REQUEST why
      | MethodNotAllowed why ->
        RequestErrors.METHOD_NOT_ALLOWED why 
      | InternalError why ->
        ServerErrors.INTERNAL_ERROR why

    handler |> replyChannel.Reply 

    return! loop()
    }

  loop()
)
