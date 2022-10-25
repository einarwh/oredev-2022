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

// let personHandler =
//     fun (next : HttpFunc) (ctx : HttpContext) ->
//         task {
//             let! person = ctx.BindModelAsync<Person>()
//             return! json person next ctx
//         }

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
    { properties = { title = "Agent vs Agent"; description = "Welcome to the HyperAgents game!" }
      actions = getStartPlayerActions
      links = [ selfLinkTo "start" ] }
  doc

let startHandler : HttpHandler =
  fun (next : HttpFunc) (ctx : HttpContext) ->
    let jsonStrPerhaps = get ctx |> Json.serialize |> Json.format
    Successful.OK jsonStrPerhaps next ctx

let startThing : HttpHandler =
  fun (next : HttpFunc) (ctx : HttpContext) ->
    task {
      let! result = StartResource.agentRef.PostAndAsyncReply(fun ch -> (ctx, ch))
      return! result next ctx
    }

let webApp =
    choose [
        route "/start" >=> 
            choose [ 
                GET >=> sirenContent >=> startHandler >=> sirenContent
                POST >=> sirenContent >=> startHandler
            ]
        route "/mystart" >=> 
            choose [ 
                GET >=> sirenContent >=> startThing 
            ]
        route "/begin" >=> 
            choose [ 
                GET >=> sirenContent >=> text "GET begin"
                POST >=> sirenContent >=> text "POST begin"
            ]
        route "/" >=> GET >=> indexHandler "world"
        routef "/hello/%s" indexHandler  
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
            "http://localhost:5000",
            "https://localhost:5001")
       .AllowAnyMethod()
       .AllowAnyHeader()
       |> ignore

let configureApp (app : IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
    (match env.IsDevelopment() with
    | true  ->
        app.UseDeveloperExceptionPage()
    | false ->
        app .UseGiraffeErrorHandler(errorHandler)
            .UseHttpsRedirection())
        .UseCors(configureCors)
        .UseStaticFiles()
        .UseGiraffe(webApp)

let configureServices (services : IServiceCollection) =
    services.AddCors()    |> ignore
    services.AddGiraffe() |> ignore

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