module LtFsClient.Tests.LightTableInterop

open System
open NUnit.Framework
open FsUnit

open LtFsClient.LightTableInterop

[<TestFixture>]
type ``parse commands`` () =

    [<Test>]
    member x.``eval selected`` () =
        let cmd = """[345,"editor.eval.fsharp", {"line-ending":null,"name":"test.fs","type-name":"FSharp","path":"C:\\path\\to\\project\\test.fs","mime":"text/x-fsharp","tags":["editor.fsharp"],"code":"1 + 4","meta":{"start":10,"end":15}}]"""
        let arg = {LineEnding=None; Name="test.fs"; TypeName="FSharp"; Path="C:\\path\\to\\project\\test.fs"; Mime="text/x-fsharp"; Tags=["editor.fsharp"]; Code="1 + 4"; Meta= Some (10,15); Pos=None}
        parseRequest cmd |> should equal (Valid(345,Eval(arg)))

    [<Test>]
    member x.``close`` () =
        let cmd = """ [345,"client.close",null] """
        parseRequest cmd |> should equal (Valid(345,Close))        

    //[53,"editor.eval.fsharp",{"line-ending":null,"name":"test.fs","type-name":"F#","path":"C:\\github\\LightTable-FSharp\\fs-src\\test\\test.fs","mime":"text/x-fsharp","tags":["editor.fsharp"],"pos":{"line":2,"ch":0},"code":"1 + 4\n\nSystem.DateTime.Now\n\nArray.create 12 (byte 0)\n\n[0..2..10]\n\nlet add x y = x + y + 10\n\nadd 1 3\n"}]
