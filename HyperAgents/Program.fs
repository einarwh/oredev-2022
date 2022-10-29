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
open Utils
open Siren
open FSharp.Control.Tasks  

// ---------------------------------
// Models
// ---------------------------------

type Agent<'T> = MailboxProcessor<'T>

type Message =
    {
        Text : string
    }

let siren<'T> (dataObj : 'T) : HttpHandler =
    fun (_ : HttpFunc) (ctx : HttpContext) ->
        ctx.SetContentType "application/vnd.siren+json; charset=utf-8"
        let serializer = ctx.GetJsonSerializer()
        serializer.SerializeToBytes dataObj
        |> ctx.WriteBytesAsync    

type CustomNegotiationConfig (baseConfig : INegotiationConfig) =
    let plainText x = text (x.ToString())

    interface INegotiationConfig with

        member __.UnacceptableHandler =
            baseConfig.UnacceptableHandler

        member __.Rules =
                dict [
                    "*/*"             , json
                    "application/json", json
                    "application/xml" , xml
                    "text/xml"        , xml
                    "application/vnd.siren+json", siren
                    "text/plain"      , plainText
                ]

// ---------------------------------
// Views
// ---------------------------------

module Views =
    open Giraffe.ViewEngine

    let layout (content: XmlNode list) =
        html [] [
            head [] [
                title []  [ encodedText "HyperAgents" ]
                link [ _rel  "stylesheet"
                       _type "text/css"
                       _href "/main.css" ]
            ]
            body [] content
        ]

    let partial () =
        h1 [] [ encodedText "HyperAgents" ]

    let index (model : Message) =
        [
            partial()
            p [] [ encodedText model.Text ]
        ] |> layout

// ---------------------------------
// Web app
// ---------------------------------

let indexHandler (name : string) =
    let greetings = sprintf "Hello %s, from Giraffe!" name
    let model     = { Text = greetings }
    let view      = Views.index model
    htmlView view

let sirenContent : HttpHandler = 
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.SetContentType "application/vnd.siren+json"
        next ctx

let startHandler : HttpHandler =
  fun (next : HttpFunc) (ctx : HttpContext) ->
    task {
      let! result = StartResource.agentRef.PostAndAsyncReply(fun ch -> (ctx, ch))
      return! result next ctx
    }

let roomWithAgentHandler (roomAgent : Agent<TrappableRoomHandlerResource.HandlerRoomMessage>) : HttpHandler =
  fun (next : HttpFunc) (ctx : HttpContext) ->
    let agentColor = ctx.Request |> HttpUtils.tryReadQueryValue "agent"
    task {
      match agentColor with 
      | Choice1Of2 clr ->
        let! maybeAgent = AgentsResource.agentRef.PostAndAsyncReply(fun ch -> AgentsResource.Lookup(clr, ch))
        match maybeAgent with
        | None ->
          return! RequestErrors.BAD_REQUEST (sprintf "no such agent %s" clr) next ctx
        | Some agentAgent ->
          let! result = roomAgent.PostAndAsyncReply(fun ch -> ((ctx, clr, agentAgent), ch))
          return! result next ctx
      | Choice2Of2 x ->
        return! RequestErrors.BAD_REQUEST x next ctx 
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

let webApp =
    // "control-room"; "office"; "laboratory"; "teleport-room"; "exit-room"
    choose [
        route "/start" >=> startHandler
        route "/control-room" >=> controlRoomHandler
        route "/office" >=> officeHandler
        route "/laboratory" >=> laboratoryHandler
        route "/teleport-room" >=> teleportRoomHandler
        route "/exit-room" >=> exitRoomHandler
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