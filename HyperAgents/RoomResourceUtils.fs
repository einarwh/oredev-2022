module RoomResourceUtils

open System
open Chiron
open Siren
open Utils
open HttpUtils
open Microsoft.AspNetCore.Http

type RoomInfo = 
  { name : string
    properties : SirenProperties
    linkInfos : (string * string list) list }

let getRoomWithActions1 (req : HttpRequest) (props : SirenProperties) (self : string) (linkInfos : (string * string list) list) = 
  let qp url = url + req.QueryString.ToString()
  let links = 
    (self |> qp |> selfLinkTo) :: (linkInfos |> List.map (fun (name, rels) -> name |> qp |> sirenLinkTo rels))
  let trappableLinks = 
    links |> List.filter (fun { rel = relations; href = lnk } -> relations |> List.exists (fun r -> r = "entrance"))
  let plantBombAction = 
    { name = "plant-bomb"
      title = "Plant bomb"
      ``method`` = "POST"
      href = req |> getUrlString
      fields = []
    }
  let pickResourceName (url : string) = 
    url.Split('?') |> Seq.head |> (fun s -> s.Split('/')) |> Seq.last

  let plantBombActions =
    let noqp (uri : Uri) = new Uri(sprintf "%s://%s%s" uri.Scheme uri.Authority uri.AbsolutePath) 
    trappableLinks 
    |> List.map (fun sl -> new System.Uri(sl.href) |> noqp)
    |> List.mapi (fun i lnk -> 
      let srcResource = lnk.ToString() |> pickResourceName 
      let uri = getUri req
      let dstResource = (uri |> noqp).ToString() |> pickResourceName
      { plantBombAction with name = sprintf "%s-%d" plantBombAction.name i
                             title = sprintf "Place bomb on entrance %s => %s" srcResource dstResource  
                             fields = [ { name = "bomb-referrer"; title = None; ``type`` = "text"; value = Some (lnk.ToString())} ]})

  let doc = 
    { properties = props 
      actions = plantBombActions
      links = links }
  doc

let getRoomWithActions (req : HttpRequest) (clr : AgentColor) (roomInfo : RoomInfo) = 
  let linkInfos = ("agents/" + clr, ["me"]) :: roomInfo.linkInfos
  getRoomWithActions1 req roomInfo.properties roomInfo.name linkInfos
