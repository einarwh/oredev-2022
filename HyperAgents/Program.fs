module HyperAgents.App

open System
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Http
open Chiron
open Giraffe
open Giraffe.ViewEngine
open Utils
open Siren
open FSharp.Control.Tasks  

type Agent<'T> = MailboxProcessor<'T>

let sirenHandler (dataObj : obj) : HttpHandler = 
    fun (_ : HttpFunc) (ctx : HttpContext) ->
        ctx.SetContentType "application/vnd.siren+json; charset=utf-8"
        let doc = dataObj :?> SirenDocument
        let str = doc |> Json.serialize |> Json.format
        str |> ctx.WriteStringAsync

let sirenLinkAsHtml (link : SirenLink) : XmlNode = 
  let linkText = link.rel |> List.head
  a [ _href link.href ] [ str linkText ]

let sirenLinkAsListItem (link : SirenLink) : XmlNode = 
  li [] [ sirenLinkAsHtml link ]

(*
<form method="POST" action="/start"><input id="name" name="name" type="input" /><label for="name">What is your name, explorer?</label><br/><input type="submit" value="Start"></form>

<form method="POST" action="http://localhost:5000/start"><input id="agent" type="text"><label for="agent">agent</label><input type="submit" value="Start agent"></form>

type SirenAction = 
  { name: string
    title: string
    ``method``: string
    href: SirenHref
    fields: SirenField list }

type SirenField = 
  { name: string
    ``type``: string
    value: Json option }

*)

let sirenFieldAsInputAndLabel (field : SirenField) : XmlNode list = 
  let valueAttrs : XmlAttribute list = 
    match field.value with 
    | Some jsonValue -> 
      let strValue : string = jsonValue |> Json.serialize |> Json.format
      [ _value strValue ]
    | None -> [] 
  let attrs = [ _id field.name; _name field.name; _type field.``type`` ] @ valueAttrs
  [
    input attrs
    label [ _for field.name ] [ str field.name ]
  ]

let sirenActionAsHtml (action : SirenAction) : XmlNode = 
  let fieldThings = action.fields |> List.collect sirenFieldAsInputAndLabel
  let submitElement = input [ _type "submit"; _value action.title ]
  let formElements = fieldThings @ [ br []; submitElement ]
  form [ _method action.method; _action action.href ] formElements

let sirenPropertiesAsHtml (props : SirenProperties) : XmlNode = 
  div [] [ 
    h1 [] [ str props.title ]
    p [] [ str props.description ]
  ]

let sirenDocumentAsHtml (doc : SirenDocument) : XmlNode = 
  let linkItems = doc.links |> List.map sirenLinkAsListItem
  let linksList = ul [] linkItems
  let navigationSection = h3 [] [ str "Navigation" ] :: [ linksList ]
  let actionSection = 
    match doc.actions with 
    | [] -> [] 
    | _ -> 
      let actionHtml = doc.actions |> List.map sirenActionAsHtml
      h3 [] [ str "Actions" ] :: actionHtml
  let props = doc.properties |> sirenPropertiesAsHtml
  div [] [
    props
    p [] navigationSection
    p [] actionSection
  ]

let htmlHandler (dataObj : obj) : HttpHandler = 
    fun (_ : HttpFunc) (ctx : HttpContext) ->
        ctx.SetContentType "text/html; charset=utf-8"
        let doc = dataObj :?> SirenDocument

        let page = html [] [
          head [] []
          body [] [
            doc |> sirenDocumentAsHtml
          ]
        ]
        let str = page |> RenderView.AsString.htmlNode
        str |> ctx.WriteStringAsync


type CustomNegotiationConfig (baseConfig : INegotiationConfig) =
    let plainText x = text (x.ToString())

    interface INegotiationConfig with

        member __.UnacceptableHandler =
            baseConfig.UnacceptableHandler

        member __.Rules =
                dict [
                    "*/*", sirenHandler
                    "application/json", json
                    "application/xml", xml
                    "text/xml", xml
                    "application/vnd.siren+json", sirenHandler
                    "text/html", htmlHandler
                    "text/plain", plainText
                ]

// ---------------------------------
// Web app
// ---------------------------------

let startHandler : HttpHandler =
  fun (next : HttpFunc) (ctx : HttpContext) ->
    task {
      let! result = StartResource.agentRef.PostAndAsyncReply(fun ch -> (ctx, ch))
      return! result next ctx
    }

let roomWithAgentHandler (roomAgent : Agent<TrappableRoomHandlerResource.HandlerRoomMessage>) : HttpHandler =
  fun (next : HttpFunc) (ctx : HttpContext) ->
    let maybeAgentColor = ctx.TryGetQueryStringValue "agent"
    task {
      match maybeAgentColor with 
      | Some clr ->
        let! maybeAgent = AgentsResource.agentRef.PostAndAsyncReply(fun ch -> AgentsResource.Lookup(clr, ch))
        match maybeAgent with
        | None ->
          return! RequestErrors.BAD_REQUEST (sprintf "no such agent %s" clr) next ctx
        | Some agentAgent ->
          let! result = roomAgent.PostAndAsyncReply(fun ch -> ((ctx, clr, agentAgent), ch))
          return! result next ctx
      | None ->
        return! RequestErrors.BAD_REQUEST "missing agent query parameter" next ctx 
    }  

let controlRoomHandler : HttpHandler =
  roomWithAgentHandler ControlRoomResource.agentRef

let officeHandler : HttpHandler =
  roomWithAgentHandler OfficeResource.agentRef

let laboratoryHandler : HttpHandler =
  roomWithAgentHandler LaboratoryResource.agentRef

let teleportRoomHandler : HttpHandler =
  roomWithAgentHandler TeleportRoomResource.agentRef

let exitRoomHandler : HttpHandler =
  roomWithAgentHandler ExitRoomResource.agentRef

let agentHandler (agentResourceColor : string) : HttpHandler = 
  fun (next : HttpFunc) (ctx : HttpContext) ->
    let requestingAgentColor = ctx.TryGetQueryStringValue "agent"
    task {
      match requestingAgentColor with 
      | Some clr ->
        let! maybeRequestingAgent = AgentsResource.agentRef.PostAndAsyncReply (fun ch -> AgentsResource.Lookup(clr, ch))
        match maybeRequestingAgent with
        | None ->
          return! RequestErrors.BAD_REQUEST (sprintf "no such agent %s" clr) next ctx
        | Some requestingAgentAgent ->
          let! maybeAgentResource = AgentsResource.agentRef.PostAndAsyncReply (fun ch -> AgentsResource.Lookup(agentResourceColor, ch))
          match maybeAgentResource with 
          | None ->
            return! RequestErrors.NOT_FOUND (sprintf "no such agent %s" agentResourceColor) next ctx
          | Some agentResource ->
            let! result = agentResource.PostAndAsyncReply(fun ch -> AgentResource.WebMessage((ctx, clr), ch))
            return! result next ctx
      | None ->
        return! RequestErrors.BAD_REQUEST "missing agent query parameter" next ctx 
    }

let bombHandler (bombId : int) : HttpHandler = 
  fun (next : HttpFunc) (ctx : HttpContext) ->
  task {
    printfn "Lookup bomb #%d" bombId
    let! maybeBomb = BombsResource.agentRef.PostAndAsyncReply(fun ch -> BombsResource.Lookup (bombId, ch))
    match maybeBomb with
    | None ->
      return! RequestErrors.NOT_FOUND "no such bomb" next ctx
    | Some bomb -> 
      let! result = bomb.PostAndAsyncReply(fun ch -> BombResource.WebMessage(ctx, ch))
      return! result next ctx
  }

let agentsHandler : HttpHandler = 
  fun (next : HttpFunc) (ctx : HttpContext) ->
    task {
      match ctx.TryGetQueryStringValue "agent" with
      | Some agentColor -> 
        printfn "Try lookup of %s" agentColor
        let! maybeAgent = AgentsResource.agentRef.PostAndAsyncReply(fun ch -> AgentsResource.Lookup (agentColor, ch))
        match maybeAgent with 
        | None ->
          return! RequestErrors.NOT_FOUND "no" next ctx
        | Some agent ->
          let! result = agent.PostAndAsyncReply (fun ch -> AgentResource.WebMessage((ctx, agentColor), ch))
          return! result next ctx
      | None ->
        return! RequestErrors.BAD_REQUEST "missing agent query parameter" next ctx 
    }

let secretFileHandler : HttpHandler =
  fun (next : HttpFunc) (ctx : HttpContext) ->
    let maybeAgentColor = ctx.TryGetQueryStringValue "agent"
    task {
      match maybeAgentColor with 
      | Some clr ->
        let! maybeAgent = AgentsResource.agentRef.PostAndAsyncReply(fun ch -> AgentsResource.Lookup(clr, ch))
        match maybeAgent with
        | None ->
          return! RequestErrors.BAD_REQUEST (sprintf "no such agent %s" clr) next ctx
        | Some agentAgent ->
          let! result = SecretFileResource.agentRef.PostAndAsyncReply(fun ch -> SecretFileResource.WebMessage((ctx, clr), ch))
          return! result next ctx
      | None ->
        return! RequestErrors.BAD_REQUEST "missing agent query parameter" next ctx 
    }    

let planeHandler : HttpHandler =
  fun (next : HttpFunc) (ctx : HttpContext) ->
    task {
      let! result = PlaneResource.agentRef.PostAndAsyncReply (fun ch -> (ctx, ch))
      return! result next ctx
    }

let webApp =
    // "control-room"; "office"; "laboratory"; "teleport-room"; "exit-room"
    choose [
        route "/start" >=> startHandler
        route "/control-room" >=> controlRoomHandler
        route "/office" >=> officeHandler
        route "/laboratory" >=> laboratoryHandler
        route "/teleport-room" >=> teleportRoomHandler
        route "/exit-room" >=> exitRoomHandler
        routef "/agents/%s" (fun ag -> agentHandler ag)
        routef "/bombs/%i" (fun id -> bombHandler id)
        route "/agents" >=> agentsHandler
        route "/secret-file" >=> secretFileHandler
        route "/plane" >=> planeHandler
        route "/" >=> redirectTo false (linkTo "start")
        setStatusCode 404 >=> text "Not Found" ]

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

let configureCors (builder : CorsPolicyBuilder) =
    builder
        .WithOrigins(
            "http://localhost:5000")
            // ,
            // "https://localhost:5001")
       .AllowAnyMethod()
       .AllowAnyHeader()
       |> ignore

let configureApp (app : IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
    (match env.IsDevelopment() with
    | true  ->
        app.UseDeveloperExceptionPage()
    | false ->
        app .UseGiraffeErrorHandler(errorHandler))
            // .UseHttpsRedirection())
        .UseCors(configureCors)
        .UseStaticFiles()
        .UseGiraffe(webApp)

let configureServices (services : IServiceCollection) =
    services.AddCors()    |> ignore
    services.AddGiraffe() |> ignore
    services.AddSingleton<INegotiationConfig>(
        CustomNegotiationConfig(
            DefaultNegotiationConfig())
    ) |> ignore

let configureLogging (builder : ILoggingBuilder) =
    builder.AddConsole()
           .AddDebug() |> ignore

[<EntryPoint>]
let main args =
    let contentRoot = Directory.GetCurrentDirectory()
    let webRoot     = Path.Combine(contentRoot, "WebRoot")
    Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(
            fun webHostBuilder ->
                webHostBuilder
                    .UseContentRoot(contentRoot)
                    .UseWebRoot(webRoot)
                    .Configure(Action<IApplicationBuilder> configureApp)
                    .ConfigureServices(configureServices)
                    .ConfigureLogging(configureLogging)
                    |> ignore)
        .Build()
        .Run()
    0