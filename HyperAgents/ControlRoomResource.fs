module ControlRoomResource

open Utils
open TrappableRoomHandlerResource

let roomInfo : RoomResourceUtils.RoomInfo = 
  { name = "control-room"
    properties = 
      { title = "The control room." 
        description = "The room you have entered is full of screens, buttons, flashing lights and beeping. Should you press a button? Which one?" }
    linkInfos = 
      [ ("office", ["office"; "entrance"])
        ("laboratory", ["laboratory"; "entrance"])
        ("exit-room", ["exit-room"; "entrance"]) ]
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
