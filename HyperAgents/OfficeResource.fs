module OfficeResource

open Utils
open TrappableRoomHandlerResource

let roomInfo : RoomResourceUtils.RoomInfo = 
  { name = "office"
    properties = 
      { title = "The office." 
        description = "You're in an office. There are screens on the walls. You see charts and burndowns. Your old arch-nemesis. Jira." }
    linkInfos = 
      [ ("laboratory", ["laboratory"; "entrance"])
        ("control-room", ["control-room"; "entrance"]) ]
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
