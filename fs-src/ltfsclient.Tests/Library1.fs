module ltfsclient.Tests

open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Threading
open FSharp.Data.Json
open FSharp.Data
open Microsoft.FSharp.Compiler.Interactive.Shell
open NUnit.Framework
open FsUnit

let fsiPath = @"C:\Program Files (x86)\Microsoft SDKs\F#\3.0\Framework\v4.0\Fsi.exe"

let init (fsiPath: string) =
    let sbOut = new Text.StringBuilder()
    let sbErr = new Text.StringBuilder()
    let inStream = new StringReader("")
    let outStream = new StringWriter(sbOut)
    let errStream = new StringWriter(sbErr)

    let argv = [| fsiPath |]
    let allArgs = Array.append argv [|"--noninteractive"|]

    let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()
    let fsiSession = FsiEvaluationSession(fsiConfig, allArgs, inStream, outStream, errStream)  
    fsiSession

let eval (fsiSession:FsiEvaluationSession) text =
    match fsiSession.EvalExpression(text) with
    | Some value -> Some (sprintf "%A" value.ReflectionValue)
    | None -> Some (sprintf "Got no result!")

let evalInteraction (fsiSession:FsiEvaluationSession) text = 
    fsiSession.EvalInteraction(text)

let tryEval (fsiSession:FsiEvaluationSession) text  =
    try
        eval fsiSession text
    with e ->
        evalInteraction fsiSession text
        Some ""

[<TestFixture>]
type ``evaluation`` () =

    [<Test>]
    member x.``eval values`` () =
       let fsi = init fsiPath
       eval fsi "1+3" |> should equal "4"

    [<Test>]
    member x.``eval binding`` () =
       let fsi = init fsiPath
       fsi.EvalInteraction("let add x y = x + y")
       eval fsi "add" |> should equal "val add : x:int -> y:int -> int"

    [<Test>]
    member x.``eval binding ok`` () =
       let fsi = init fsiPath
       tryEval fsi "let add x y = x + y" |> should equal ""

    [<Test>]
    member x.``eval binding info`` () =
       let fsi = init fsiPath
       fsi.EvalInteraction("let add x y = x + y")
       printfn "%A" (eval fsi "add")
       printfn "%A" (eval fsi "add 5 6")
