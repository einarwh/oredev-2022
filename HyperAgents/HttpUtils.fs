module HttpUtils

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
