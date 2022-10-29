module TrappableRoomHandlerResource

open System

open Utils
open Siren
open BombResource
open RoomResourceUtils
open TrappableRoomResource
open Giraffe
open HttpMethods
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Primitives 

type HandlerRoomRequestInfo = HttpContext * AgentColor * Agent<AgentResource.Message>

type HandlerRoomMessage = HandlerRoomRequestInfo * AsyncReplyChannel<HttpHandler>

let createAgent (roomInfo : RoomInfo) = 
  Agent<HandlerRoomMessage>.Start (fun inbox ->
  let trappableRoomAgent = TrappableRoomResource.createAgent roomInfo
  let rec loop() = async {
    let! (input, replyChannel) = inbox.Receive()
    let! res = trappableRoomAgent.PostAndAsyncReply(fun ch -> (input, ch))
    let handler = 
      match res with 
      | Ok doc ->
        Successful.OK doc 
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
