
#I "../packages/FSharp.Compiler.Service.0.0.11-alpha/lib/net40"
#I "../packages/FSharp.Data.1.1.10/lib/net40"
#r "FSharp.Compiler.Service.dll"
#r "FSharp.Data.dll"

open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Threading
open FSharp.Data.Json
open FSharp.Data
open Microsoft.FSharp.Compiler.Interactive.Shell

let argv = fsi.CommandLineArgs 

//Log on file and console
printfn "command line args: %A" argv

let pid = lazy (System.Diagnostics.Process.GetCurrentProcess().Id)
let m = lazy ( 
    let createNew = false; 
    new Mutex(false, "lt-fs-runner-log", ref createNew) )
let lockLogfile () = { new IDisposable with member x.Dispose() = m.Value.ReleaseMutex() }

let logFilePath = Path.Combine(__SOURCE_DIRECTORY__,"log.txt")

printfn "pid %i" pid.Value 
printfn "log file '%s'" logFilePath

let logToFile s =
    use mine = lockLogfile ()
    m.Value.WaitOne() |> ignore
    let line = sprintf "[%i] %s%s" pid.Value s Environment.NewLine
    File.AppendAllText(logFilePath, line)

let logStrat s =
    printfn "%s" s 
    logToFile s

let logf f = Printf.ksprintf logStrat f
let log = logf "%s"

// FSI
let mutable fsiExePath : string option = None

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

let fsi = lazy (
    match fsiExePath with
    | Some x -> init x
    | None -> failwith "fsi path not set" 
    )

let evalExpr (fsiSession:FsiEvaluationSession) text =
    match fsiSession.EvalExpression(text) with
    | Some value -> (sprintf "%A" value.ReflectionValue)
    | None -> (sprintf "Got no result!")

let evalInteraction (fsiSession:FsiEvaluationSession) text = 
    fsiSession.EvalInteraction(text)

type Evaluation =
    | Success
    | Exception of string
    | Result of string

let eval text  =
    let fsiSession = fsi.Value
    try
        let v = evalExpr fsiSession text
        Evaluation.Result(v)
    with e ->
        try
            evalInteraction fsiSession text
            Evaluation.Success
        with e ->
            let exInfo = [e.Message; e.StackTrace.Replace("\r\n", "\n") ] |> List.fold (fun s x -> x + "\n" + s) ""
            Evaluation.Exception(exInfo)


// LightTable

let lt_encoding = System.Text.UTF8Encoding()

let write (stream: Stream) (message: string) = 
    let msg = message + "\n"
    log "Sending message:"
    log msg
    let data = lt_encoding.GetBytes(msg)         
    stream.Write(data, 0, data.Length)
    stream.Flush()
    log "Sent message:"

let read (stream: Stream) =
    let data = Array.create 1024 (byte 0)
    let bytes = stream.Read(data, 0, data.Length)
    logf "Received bytes: %A" data.[0 .. bytes]
    let responseData = lt_encoding.GetString(data, 0, bytes)
    logf "Received: %A" responseData
    responseData

let unixDirSeparator (path: string) = path.Replace("\\", "/")

type Request = 
    | Valid of int * string * string option
    | Malformed of string

type Response = {ClientId: int; Cmd: string; Args: JsonValue}

type Cmd = JsonProvider<""" [63,"editor.eval.fsharp",null] """>

let read_request stream handler =
    log "Waiting for request ..."

    let requestRaw = read stream
    log requestRaw

    let request =
        try
            let a = Cmd.Parse requestRaw
            match a.JsonValue with
            | JsonValue.Array [| JsonValue.Number(cliIdl); JsonValue.String(cmd); args|] -> 
                let a = if args = JsonValue.Null then None else Some (args.ToString())
                Request.Valid((int cliIdl), cmd, a)
            | _ -> Malformed(requestRaw)
        with e -> Malformed(requestRaw)

    handler stream request

let send_response stream response =
    let mex = JsonValue.Array([| JsonValue.Number(decimal response.ClientId); JsonValue.String(response.Cmd); response.Args |])
    write stream (mex.ToString())

// Commands

type EvalFSharpSelectedCmdArgs = JsonProvider<"""
    {"line-ending":null,"name":"test.fs","type-name":"FSharp","path":"C:\\path\\to\\project\\test.fs","mime":"text/x-fsharp","tags":["editor.fsharp"],"code":"1 + 4","meta":{"start":0,"end":0}}
""">

let (|EvalFSharpSelectedCmd|_|) request =
    match request with
    | Valid(clientId, "editor.eval.fsharp", Some args) ->
        try Some (clientId, (EvalFSharpSelectedCmdArgs.Parse(args)))
        with e -> None
    | _ -> None

type EvalFSharpNoSelectionCmdArgs = JsonProvider<""" 
    {"line-ending":null,"name":"test.fs","type-name":"FSharp","path":"C:\\path\\to\\project\\test.fs","mime":"text/x-fsharp","tags":["editor.fsharp"],"pos":{"line":2,"ch":0},"code":"1 + 4\n\n3\n\n\nlet add x y = x + y\n\n34\n\n3 * 4\n\n"}
""">

let (|EvalFSharpNoSelectionCmd|_|) request =
    match request with
    | Valid(clientId, "editor.eval.fsharp", Some args) ->
        try Some (clientId, (EvalFSharpNoSelectionCmdArgs.Parse(args)))
        with e -> None
    | _ -> None



let (|CloseCmd|_|) request =
    match request with
    | Valid(clientId, "client.close", None) ->
        Some clientId
    | _ -> None

let rec handle stream request =
    log "handle request:"
    logf "%A" request

    let responseAndLoop response =
        log "Sending eval response"
        logf "%A" response
        send_response stream response
        log "Sent Eval response"        
        read_request stream handle

    match request with
    | Malformed s ->
        log "Received malformed request, aborting"
        log s |> ignore

    | CloseCmd clientId ->
        logf "Client %i closed" clientId |> ignore

    | EvalFSharpSelectedCmd (clientId, args) ->
        log "Eval cmd selected"
        let cmd, res =
            match eval (args.Code) with
            | Evaluation.Success ->
                ("editor.eval.fsharp.success", JsonValue.Object( Map.ofList [("meta", args.Meta.JsonValue)] ))
            | Evaluation.Result(result) ->
                ("editor.eval.fsharp.result", JsonValue.Object( Map.ofList [("result", JsonValue.String(result)); ("meta", args.Meta.JsonValue)] ))
            | Evaluation.Exception(exInfo) ->
                ("editor.eval.fsharp.exception", JsonValue.Object( Map.ofList [("ex", JsonValue.String(exInfo)); ("meta", args.Meta.JsonValue)] ))
        responseAndLoop {ClientId = clientId; Cmd = cmd; Args = res}

    | EvalFSharpNoSelectionCmd (clientId, args) ->
        log "Eval cmd not selected"
        let cmd, res =
            match eval (args.Code) with
            | Evaluation.Success ->
                ("editor.eval.fsharp.success", JsonValue.Object( Map.ofList [("meta", JsonValue.Null)] ))
            | Evaluation.Result(result) ->
                ("editor.eval.fsharp.result", JsonValue.Object( Map.ofList [("result", JsonValue.String(result)); ("meta", JsonValue.Null)] ))
            | Evaluation.Exception(exInfo) ->
                ("editor.eval.fsharp.exception", JsonValue.Object( Map.ofList [("ex", JsonValue.String(exInfo)); ("meta", JsonValue.Null)] ))
        responseAndLoop {ClientId = clientId; Cmd = cmd; Args = res}

    | Valid (clientId, cmd, args) ->
        logf "Cmd '%s' unsupported (args '%s') " cmd (args.ToString()) |> ignore


let startClient (ipAddress:IPAddress) port clientId =
    log "Initializing FSI... "
    let fsiSession = fsi.Value
    log "Done"

    let client = new TcpClient()

    client.Connect(ipAddress, port)
    logf "Connected to %A on port %d" ipAddress port

    use stream = client.GetStream()

    let pwd = System.Environment.CurrentDirectory

    let client_info = 
        let props =
            [ ("name", JsonValue.String( Path.GetFileName(pwd) |> unixDirSeparator ) );
              ("client-id", JsonValue.Number( decimal clientId ) );
              ("dir", JsonValue.String( pwd |> unixDirSeparator ) );
              ("commands", JsonValue.Array( [|JsonValue.String("editor.eval.fsharp") |]));
              ("type", JsonValue.String("fsharp")); ]
        JsonValue.Object (Map.ofList props)

    let initialResponse = client_info.ToString()

    log "Client info:"
    log initialResponse
    log "Sending Client info..."

    write stream initialResponse

    log "Sent Client info"

    read_request stream handle



let (|Int|_|) x =
    match System.Int32.TryParse(x) with
    | (false, _) -> None
    | (true, v) -> Some v




logToFile (sprintf "command line args: %A" argv)
logToFile (sprintf "pid %i" pid.Value)
logToFile (sprintf "log file '%s'" logFilePath)

let main port clientId fsiPath =
    try
        fsiExePath <- Some fsiPath
        startClient (Net.IPAddress.Loopback) port clientId
        log "bye!"
        0
    with e ->
        logf "Ouch! %A" e
        2

let exitCode =
    match argv with
    | [| fsx; Int(x); Int(y); p |] ->
        main x y p
    | _ -> 
        [ "invalid arguments"; ""; "usage: cli PORT CLIENT_ID FSI_PATH"; ] |> List.iter (printfn "%s")
        1

System.Environment.Exit(exitCode);


