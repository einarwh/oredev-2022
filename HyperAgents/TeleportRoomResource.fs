module TeleportRoomResource

open Utils
open TrappableRoomHandlerResource

let roomInfo : RoomResourceUtils.RoomInfo = 
  { name = "teleport"
    properties = 
      { title = "The teleportation room." 
        description = "You're in the teleportation room. Lo and behold, there a teleportation device here. Who would have guessed?" }
    linkInfos = 
      [ ("laboratory", ["laboratory"; "entrance"])
        ("exit-room", ["use-device"]) ]
  }

let agentRef = Agent<HandlerRoomMessage>.Start (fun inbox ->
  let trappableRoomAgent = createAgent roomInfo
  let rec loop() = async {
    let! (input, replyChannel) = inbox.Receive()
    let! response = trappableRoomAgent.PostAndAsyncReply(fun ch -> (input, ch))
    response |> replyChannel.Reply
    return! loop()
    }

  loop()
)
