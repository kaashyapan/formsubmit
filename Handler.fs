namespace FormSubmit

open Amazon.Lambda.Core
open Amazon.Lambda.APIGatewayEvents
open Amazon
open Amazon.SimpleEmail
open Amazon.SimpleEmail.Model
open Amazon.SimpleNotificationService
open Amazon.SimpleNotificationService.Model
open FSharp.Data
open FSharp.Data.JsonExtensions

[<assembly:LambdaSerializer(typeof<Amazon.Lambda.Serialization.Json.JsonSerializer>)>]
do ()

open System.IO
open System.Text
open System.Net
open System.Collections.Generic

module SNS =
    let sendSMS(request: APIGatewayProxyRequest, response: APIGatewayProxyResponse )  =
        match request.QueryStringParameters.TryGetValue("phone") with
        | (true, phone) ->
            let awsclient = new AmazonSimpleNotificationServiceClient()
            let pubr = PublishRequest()
            let sender = MessageAttributeValue()
            sender.StringValue <- "HTEAM"
            sender.DataType <- "String"
            let smstype = MessageAttributeValue()
            smstype.StringValue <- "Transactional"
            smstype.DataType <- "String"
            pubr.MessageAttributes.["AWS.SNS.SMS.SenderID"] <- sender
            pubr.MessageAttributes.["AWS.SNS.SMS.SMSType"]  <- smstype
            pubr.Message <- "You have an email on heartteamindia.com"
            pubr.PhoneNumber <- phone
            let resp = awsclient.PublishAsync(pubr)
            let sendAsync pubr =
                async {
                    let! response = awsclient.PublishAsync(pubr) |> Async.AwaitTask
                    return response
                    }

            let resp = pubr |> sendAsync |> Async.RunSynchronously

            printfn "SES return code was %A" resp.HttpStatusCode

            if resp.HttpStatusCode = HttpStatusCode.OK then
                response.StatusCode <- 200
            else
                response.StatusCode <- 501
                response.Body <- "SNS returned a bad response"

        | _ ->
            response.StatusCode <- 202

        response

module SES = 
    let createHtmlMsg(msg:string): string =
        let prefix = """
        <!DOCTYPE html>
        <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta http-equiv="X-UA-Compatible" content="IE=edge">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <link rel="stylesheet" href="//cdn.rawgit.com/milligram/milligram/master/dist/milligram.min.css">
            </head>
            <body>
        """

        let postfix = """
        </body>
        </html>
        """
        prefix + msg + postfix


    let processItems(value) =
        match value with
        | JsonValue.String(str) -> str
        | JsonValue.Number(num) -> string num
        | JsonValue.Float(cnum) -> string cnum
//        | JsonValue.Array(arr) -> arr |> printArray
        | _ ->  @"<span style='color: red'>Value was not recognizable</span>"

    let processMsg(body: string): string =
        let jsonBody =  JsonValue.Parse(body)
        let props = jsonBody.Properties
        let mutable proplist = List.empty
        for (key, val1) in props do
            let tup = processItems(val1)
            let item = "<strong>" + key + " - " + "</strong>" + tup + "<br/>"
            proplist <- List.append proplist [item]

        proplist |> System.String.Concat |> createHtmlMsg

    let sendEmail(req: APIGatewayProxyRequest, resp: APIGatewayProxyResponse )  =
        let request =  req
        let response =  resp
        printfn "req %A" req
        printfn "request %A" request
        printfn "request %A" request
        printfn "Query strings were %A" request.QueryStringParameters

        match request.QueryStringParameters.TryGetValue("email") with
        | (true, email) ->

            let htmlMsg = request.Body |> processMsg

            let mailClient = new AmazonSimpleEmailServiceClient()
            let mailBody = Amazon.SimpleEmail.Model.Body()
            let bodyContent = Amazon.SimpleEmail.Model.Content()
            let subject = Amazon.SimpleEmail.Model.Content()
            subject.Data <- "Message from heartteamindia.com"
            bodyContent.Data <- htmlMsg
            mailBody.Html <- bodyContent

            let dest = Amazon.SimpleEmail.Model.Destination()
            let toAddresses = new List<string>()
            toAddresses.Add(email)
            dest.ToAddresses <- toAddresses 

            let msg = Amazon.SimpleEmail.Model.Message()
            msg.Subject <-  subject
            msg.Body <-  mailBody

            let emailRqst = Amazon.SimpleEmail.Model.SendEmailRequest()
            emailRqst.Destination <- dest
            emailRqst.Message <- msg
            emailRqst.Source <- "sunder.narayanaswamy@gmail.com"

            let sendAsync rqst = 
                async {
                    let! response = mailClient.SendEmailAsync(rqst) |> Async.AwaitTask
                    return response
                    }

            let resp = emailRqst |> sendAsync |> Async.RunSynchronously

            printfn "SES return code was %A" resp.HttpStatusCode

            if resp.HttpStatusCode = HttpStatusCode.OK then
                response.StatusCode <- 200
                response.Body <- response.Body + "|" + resp.MessageId
            else
                response.StatusCode <- 501
                response.Body <- "SES returned a bad response"

        | _ ->
            response.StatusCode <- 502
            response.Body <- "No target address was specified in the query string"

        response

module Handler = 
    let accept(request:APIGatewayProxyRequest, context:ILambdaContext): APIGatewayProxyResponse = 
        printfn "Query strings were %A" request.QueryStringParameters
        printfn "Body was %A" request.Body

        let response = APIGatewayProxyResponse()
        let headers = new Dictionary<string, string>()
        headers.Add("Content-Type", "text/plain")
        response.Headers <- headers

        match request.QueryStringParameters with
        | null ->
            response.StatusCode <- 503
            response.Body <- "No target address was specified in the query string"
            response
        | _ ->
            let sesResponse = SES.sendEmail(request, response)

            if sesResponse.StatusCode < 250 then
                SNS.sendSMS(request, sesResponse)
            else
                sesResponse
