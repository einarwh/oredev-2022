module UploadResource

open Chiron

open Utils
open Siren
open Giraffe
open HttpMethods
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Primitives 

type Agent<'T> = MailboxProcessor<'T>
type RequestInfo = HttpContext
type ResponseInfo =
      | Success of SirenDocument
      | Failure

type Message = WebMessage of RequestInfo * AsyncReplyChannel<HttpHandler> | UploadCompleteNotification

