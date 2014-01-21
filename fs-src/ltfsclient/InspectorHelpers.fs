module LightTable.FSharp.Inspector.Helpers

module private Utils =
    open System
    open System.IO
    open System.Drawing

    let createGif (delay : TimeSpan) images =
        let extension id blocks =
            let extIntroducer = byte 0x21
            let terminator = byte 0
            let data =
                blocks
                |> List.map (fun (x: byte []) -> Array.append [| byte x.Length |] x)
                |> Array.concat
            Array.concat [ [| extIntroducer; id |];
                            data;
                            [| terminator |] ]

        let applicationExtension =
            [ Text.ASCIIEncoding.ASCII.GetBytes("NETSCAPE2.0");
                [| 1uy; 0uy; 0uy |] ]
            |> extension (byte 0xFF)

        let graphicControlextension =
            let flags = byte 9 //reserved, disposal method, user input, transparent color
            let transparentColorIndex = byte 0xFF
            let delayTime =
                delay
                |> (fun t -> t.TotalMilliseconds / 10.0)
                |> int
            [ [| flags;
                 byte <| (delayTime &&& 0xFF);
                 byte <| (delayTime >>> 8);
                 transparentColorIndex |] ]
            |> extension (byte 0xF9)

        let outputStream = new MemoryStream()
        use binaryWriter = new BinaryWriter(outputStream)

        let addFrame i (image : Image) =
            let memoryStream = new MemoryStream()
            image.Save(memoryStream, Imaging.ImageFormat.Gif)
            let raw = memoryStream.ToArray()
            if i = 0 then
                //Header & global color table
                binaryWriter.Write(raw, 0, 781)
                binaryWriter.Write(applicationExtension)
            binaryWriter.Write(graphicControlextension)
            //Image data
            binaryWriter.Write(raw, 789, raw.Length - 790)

        images |> Seq.iteri addFrame

        let imageTerminator = ";"
        binaryWriter.Write(imageTerminator)
        //copy to another stream, because BinaryWriter dispose stream
        outputStream.Position <- 0L
        Image.FromStream(outputStream)

module Gif =
    let create = Utils.createGif
