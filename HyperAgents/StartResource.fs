module StartResource

open System

open Utils
open Siren
open Giraffe
open HttpUtils
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Primitives 

type RequestInfo = HttpContext
type ResponseInfo =
      | Success of SirenDocument
      | Failure

type Message = RequestInfo * AsyncReplyChannel<HttpHandler>
type Color = string

type StartPlayerResult = Started of Uri * Color | FailedToStart

let getStartPlayerActions =
  let agentField = { name = "agent"; ``type`` = "text"; value = None }

  let startAgentAction = 
    { name = "start-agent"
      ``method`` = "POST"
      title = "Start agent"
      href = linkTo "start"
      fields = [ agentField ] }
  [ startAgentAction ]

let get ctx =  
  let doc = 
    { properties = { title = "Agent vs Agent"; description = "Welcome to the HyperAgents game!!" }
      actions = getStartPlayerActions
      links = [ selfLinkTo "start" ] }
  doc

let r = new System.Random()

let getRandomStartLocation() : string =
  let locations = [ "control-room"; "office"; "laboratory"; "teleport-room"; "exit-room" ]
  let roomIndex = r.Next(List.length locations)
  locations.Item roomIndex |> linkTo

let start (agent : Color) =
  let qs = sprintf "agent=%s" agent
  let loc = getRandomStartLocation() |> toUri |> withQueryString qs
  Started (loc, agent)

let startPlayer (ctx : HttpContext) =
  match ctx.GetFormValue "agent" with
  | Some agent -> start agent
  | None -> FailedToStart

let agentRef = Agent<Message>.Start (fun inbox ->
  let rec start() = async {
    let! msg = inbox.Receive()
    let (ctx, replyChannel) = msg

    let (state, handler) =
      match httpMethodFor ctx.Request with
      | Some GET -> 
        (start, get ctx |> Successful.OK)
      | Some POST ->
        match startPlayer ctx with
        | Started (loc, color) ->
          AgentsResource.agentRef.Post(AgentsResource.Register(color, AgentResource.createAgent color loc))
          (start, redirectTo false <| loc.ToString())
        | FailedToStart ->
          (start, RequestErrors.BAD_REQUEST "no")
      | _ -> 
        (start, RequestErrors.METHOD_NOT_ALLOWED "no")
    handler |> replyChannel.Reply
    return! state() }

  start()
)
