module HttpMethods

open System
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Primitives 

type HttpMethod = 
    GET 
    | POST 

let getUrlString (req : HttpRequest) : string = 
    $"{req.Scheme}://{req.Host}{req.PathBase}{req.Path}{req.QueryString}"

let getUri (req : HttpRequest) : Uri = 
    new Uri(getUrlString req)

let httpMethodFor (req : HttpRequest) : HttpMethod option = 
   let methodStr = req.Method 
   if methodStr = "GET" then Some GET 
   else if methodStr = "POST" then Some POST
   else None 

let tryReadFormValue (key : string) (req : HttpRequest) : Choice<string, string> = 
  let v = req.Form.[key]
  if StringValues.IsNullOrEmpty v then Choice2Of2 "no such key"
  else Choice1Of2 v.[0]

let tryReadHeaderValue (headerName : string) (req : HttpRequest) : Choice<string, string> = 
    let v = req.Headers.[headerName]
    if StringValues.IsNullOrEmpty v then Choice2Of2 "no such header"
    else Choice1Of2 v.[0]

let tryReadQueryValue (key : string) (req : HttpRequest) : Choice<string, string> = 
  let v = req.Query.[key]
  if StringValues.IsNullOrEmpty v then Choice2Of2 "no such key"
  else Choice1Of2 v.[0]
