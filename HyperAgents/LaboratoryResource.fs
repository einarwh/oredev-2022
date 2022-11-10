module LaboratoryResource

open Utils
open TrappableRoomHandlerResource

let roomInfo : RoomResourceUtils.RoomInfo = 
  { name = "laboratory"
    properties = 
      { title = "The laboratory." 
        description = "You're in a secret research laboratory. Gadgets are whizzing." }
    linkInfos = 
      [ ("teleport-room", ["teleport-room"; "entrance"])
        ("control-room", ["control-room"; "entrance"])
        ("office", ["office"; "entrance"]) ]
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
