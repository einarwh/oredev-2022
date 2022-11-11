module BombResource

open System
open Siren
open Utils
open Giraffe
open HttpUtils
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Extensions 
open Microsoft.Extensions.Primitives 

type Message =
  | WebMessage of HttpContext * AsyncReplyChannel<HttpHandler>
  | TriggerNotification
  | AliveQuery of AsyncReplyChannel<bool>

type CutWireResult = Disarmed of SirenDocument | Explosion of SirenDocument
type DisarmAttemptResult =
      | Valid of CutWireResult
      | Invalid

type BombInfo = 
  { id : int
    referrer : string
    agent : Agent<Message>
  }

let getUrl (req : HttpRequest) : string = 
    req.GetEncodedUrl()

let getDisarmed (ctx : HttpContext) (backUrl : Uri) : SirenDocument = 
  let url = ctx.Request |> getUrl 
  { properties = 
      { title = "Disarmed bomb"
        description = "You have successfully disarmed the bomb using your immensive skill!" }
    actions = []
    links = 
      [ { rel = [ "self" ]; href = url.ToString() } 
        { rel = [ "back" ]; href = backUrl.ToString() } ] }

let getExplosion (ctx : HttpContext) (backUrl : Uri) : SirenDocument = 
  let url = ctx.Request |> getUrl 
  { properties = 
      { title = "Exploding bomb"
        description = "*** BOOM ***" }
    actions = []
    links = 
      [ { rel = [ "self" ]; href = url.ToString() } 
        { rel = [ "back" ]; href = backUrl.ToString() } ] }

let cutWire (ctx : HttpContext) (backUrl : Uri) (wireColor : string) : CutWireResult =
  printfn "Cutting the %s wire" wireColor
  match wireColor with
  | "red" ->
    getDisarmed ctx backUrl |> Disarmed
  | _ ->
    getExplosion ctx backUrl |> Explosion 

let attemptDisarm (ctx : HttpContext) (backUrl : Uri) : DisarmAttemptResult =  
  match ctx.GetFormValue "wire" with
  | Some wireColor -> cutWire ctx backUrl wireColor |> Valid
  | None -> Invalid

let getReady (ctx : HttpContext) = 
  let links = 
    match ctx.TryGetRequestHeader "referer" with
    | Some ref -> [ { rel = ["back"]; href = ref } ]
    | None -> []
  let doc = 
    { properties = { title = sprintf "A bomb. Hee hee."; description = "Your deviously set up bomb that is sure to catch the other agents by surprise." }
      actions = []
      links = links }
  doc

let getTriggered (ctx : HttpContext) = 
  let url = ctx.Request |> getUri
  let cutWireField = { name = "wire"; title = Some "Color of wire to cut"; ``type`` = "text"; value = None }
  let cutWireAction = 
    { name = "cut-wire"
      ``method`` = "POST"
      title = "Cut wire"
      href = link2 url.AbsolutePath url.Query
      fields = [ cutWireField ] }
  let doc = 
    { properties = 
        { title = sprintf "A bomb is ticking."; 
          description = "You have encountered a Die Hard-style scary bomb. There is some sort of liquid flowing in a container. You see a red and a blue wire." }
      actions = [ cutWireAction ]
      links = [] }
  doc

let createAgent referrer target =
  Agent<Message>.Start (fun inbox ->
  let rec ready() = async {
    printfn "bomb %s -> %s is ready..." referrer target 
    let! msg = inbox.Receive()
    match msg with
    | AliveQuery replyChannel ->
      true |> replyChannel.Reply
      return! ready()
    | TriggerNotification ->
      return! triggered()
    | WebMessage (ctx, replyChannel) ->
      let handler = 
        match httpMethodFor ctx.Request with
        | Some GET ->
          let doc = getReady ctx
          Successful.OK doc
        | _ ->
          RequestErrors.METHOD_NOT_ALLOWED "no"
      handler |> replyChannel.Reply
      return! ready() }

  and triggered() = async {
    let! msg = inbox.Receive()
    printfn "bomb %s -> %s is triggered!!!" referrer target 
    match msg with
    | AliveQuery replyChannel ->
      false |> replyChannel.Reply
      return! triggered()
    | TriggerNotification ->
      printfn "Multiple trigger!"
      (* It's OK to trigger a bomb multiple times. *)
      return! triggered()
    | WebMessage (ctx, replyChannel) ->
      match httpMethodFor ctx.Request with
      | Some GET ->
        let doc = getTriggered ctx
        Successful.OK doc |> replyChannel.Reply
        return! triggered()
      | Some POST ->
        let maybeRequestingAgentColor = ctx.TryGetQueryStringValue "agent"
        match ctx.TryGetQueryStringValue "agent" with 
        | Some requestingAgentColor -> 
          let backUrl = target |> toUri |> withoutQueryString |> withQueryAgent requestingAgentColor
          match attemptDisarm ctx backUrl with
          | Invalid ->
            RequestErrors.BAD_REQUEST "no" |> replyChannel.Reply
            return! triggered()
          | Valid outcome ->
            (* Notify: room to remove bomb. Maybe not? Just let room contain actual bomb agents, and ask for ready ones? Others are unimportant. *)
            match outcome with 
            | Disarmed doc ->
              Successful.OK doc |> replyChannel.Reply
              return! gone()
            | Explosion doc ->
              (* Notify agent of their death. *)
              let maybeAffectedAgentColor = ctx.TryGetQueryStringValue "agent"
              match maybeAffectedAgentColor with
              | Some affectedAgentColor ->
                printfn "Agent %s blew up." affectedAgentColor
                let! maybeAffectedAgent = AgentsResource.agentRef.PostAndAsyncReply(fun ch -> AgentsResource.Lookup(affectedAgentColor, ch))
                match maybeAffectedAgent with
                | None ->
                  printfn "Error: agent %s not found in registry." affectedAgentColor
                | Some affectedAgent ->
                  affectedAgent.Post(AgentResource.ExplodingBombNotification)
                  (* The agent drops the secret file when they die. *)
                  target |> toUri |> SecretFileResource.Dropped |> SecretFileResource.agentRef.Post
                (* Redirect to random location. *)
                let randomLink = Utils.getRandomStartLocation() |> toUri |> withQueryAgent affectedAgentColor |> uri2str
                redirectTo false randomLink |> replyChannel.Reply         
              | None ->
                printfn "Error: missing query param agent."
                RequestErrors.BAD_REQUEST "missing query param agent" |> replyChannel.Reply
              return! gone() 
        | None -> 
          RequestErrors.BAD_REQUEST "missing agent query parameter" |> replyChannel.Reply 
      | _ ->
        RequestErrors.METHOD_NOT_ALLOWED "no" |> replyChannel.Reply
        return! triggered() }

  and gone() = async {
    let! msg = inbox.Receive()
    printfn "bomb %s -> %s is gone." referrer target 
    match msg with
    | AliveQuery replyChannel ->
      false |> replyChannel.Reply
      return! gone()
    | TriggerNotification ->
      printfn "Logic error: triggered a bomb that is gone (disarmed or exploded)."
      return! gone()
    | WebMessage (ctx, replyChannel) ->
      match httpMethodFor ctx.Request with
      | Some GET ->
        RequestErrors.GONE "The bomb is gone" |> replyChannel.Reply
      | _ ->
        RequestErrors.METHOD_NOT_ALLOWED "no" |> replyChannel.Reply
    return! gone() }

  ready()
)
