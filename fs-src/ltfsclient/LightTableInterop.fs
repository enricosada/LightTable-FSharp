module LightTable.FSharp.LightTableInterop

open System
open System.IO
open FSharp.Data.Json

type json = JsonValue

type EvalArgs = 
    { LineEnding : string option
      Name : string
      TypeName : string
      Path : string
      Mime : string
      Tags : string list
      Code : string
      Meta : (int * int) option
      Pos : (int * int) option }

type CmdArgs = 
    | Eval of EvalArgs
    | Close
    | Unsupported of string * json

type Request = 
    | Valid of int * CmdArgs
    | Malformed of string

type Response = 
    { ClientId : int
      Cmd : string
      Args : JsonValue }

let Encoding = System.Text.UTF8Encoding()

open FSharp.Data.Json.Extensions

let (|EvalCmd|_|) (cmd, x) = 
    if not <| (cmd = "editor.eval.fsharp") then None
    else 
        match (x?``line-ending``, x?name, x?``type-name``, x?path, x?mime, 
               x?tags, x?code) with
        | (lineEnding, json.String(name), json.String(typeName), 
           json.String(path), json.String(mime), json.Array(tags), 
           json.String(code)) -> 
            let d = 
                { LineEnding = 
                      match lineEnding with
                      | json.String(x) -> Some x
                      | _ -> None
                  Name = name
                  TypeName = typeName
                  Path = path
                  Mime = mime
                  Tags = 
                      tags
                      |> Array.map (fun x -> x.AsString())
                      |> Array.toList
                  Code = code
                  Meta = None
                  Pos = None }
            
            let pos = 
                match x.TryGetProperty("pos") with
                | Some(json.Object(p)) -> 
                    match p.TryFind("line"), p.TryFind("ch") with
                    | (Some(json.Number(line)), Some(json.Number(ch))) -> 
                        Some(int line, int ch)
                    | _ -> None
                | _ -> None
            
            let meta = 
                match x.TryGetProperty("meta") with
                | Some(json.Object(m)) -> 
                    match m.TryFind("start"), m.TryFind("end") with
                    | (Some(json.Number(inizio)), Some(json.Number(fine))) -> 
                        Some(int inizio, int fine)
                    | _ -> None
                | _ -> None
            
            Some(Eval({ d with Meta = meta
                               Pos = pos }))
        | _ -> None

let parseRequest s = 
    let parse validJson = 
        let parseArgs = 
            function 
            | EvalCmd a -> a
            | "client.close", json.Null -> Close
            | a -> Unsupported(a)
        match validJson with
        | json.Array([| json.Number(clientId); json.String(cmd); args |]) -> 
            let a = parseArgs (cmd, args)
            Valid(int clientId, a)
        | _ -> Malformed(s)
    try 
        json.Parse(s) |> parse
    with e -> Malformed(s)
