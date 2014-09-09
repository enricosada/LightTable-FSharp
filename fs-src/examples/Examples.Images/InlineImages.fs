module LightTable.FSharp.Examples.InlineImages

open System
open System.Drawing

let download url =
    use client = new Net.WebClient()
    let data = client.DownloadData(Uri(url))
    use stream = new IO.MemoryStream(data)
    Image.FromStream(stream)

let thumb (img : Image) =
    img.GetThumbnailImage(150, 150, (fun () -> false), IntPtr.Zero)

let get = download >> thumb

let imageGraphics() =
    let shape =
        let path = new Drawing2D.GraphicsPath()
        path.AddArc(10, 10, 150, 150, 135.0f, 195.0f)
        path.AddArc(140, 10, 150, 150, 210.0f, 195.0f)
        path.AddLine(32, 139, 150, 270)
        path.AddLine(267, 139, 150, 270)
        path.AddLine(267, 139, 32, 139)
        path

    let bmp = new Bitmap(300, 300)
    use g = Graphics.FromImage(bmp)
    g.FillPath(Brushes.Red, shape)
    g.DrawPath(Pens.Red, shape)
    bmp

let lt = get <| "http://www.lighttable.com/images/logo.png"

let love = imageGraphics() |> thumb

let fsharp = get <| "http://fsharp.org/img/logo.png"

let gif = download <| "http://optimalhumanmodulation.files.wordpress.com/2013/11/heroics.gif"


