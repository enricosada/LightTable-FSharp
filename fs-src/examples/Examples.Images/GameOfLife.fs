module LightTable.FSharp.Examples.GameOfLife

open System
open System.Drawing
open System.IO

type Cell =
    | Alive
    | Dead

let step cell neighbours =
    let alives =
        neighbours |> List.sumBy (function
                          | Alive -> 1
                          | Dead -> 0)
    match (cell, alives) with
    | Alive, n when n < 2 -> Dead // by under-population
    | Alive, n when n > 3 -> Dead // by overcrowding
    | Alive, 2 | Alive, 3 -> Alive // lives on
    | Dead, 3 -> Alive // by reproduction
    | Dead, _ -> Dead

let neighbours getAt (x, y) =
    [ for i in [ x - 1..x + 1 ] do
          for j in [ y - 1..y + 1 ] -> (i, j) ]
    |> List.filter (fun a -> a <> (x, y)) //not itself
    |> List.map (fun coord -> (coord, getAt <| coord))

/// World is a Map (x,y) -> Cell with all Alive cell, default is Dead cell
type World = Map<int * int, Cell>

let emptyWorld : World = Map.empty

let getAt coord (world : World) =
    match world |> Map.tryFind coord with
    | Some x -> x
    | None -> Dead

let setAt coord v (world : World) =
    match v with
    | Alive -> world |> Map.add coord v
    | Dead -> world |> Map.remove coord

let tick world =
    let at coord = world |> getAt coord

    let stepCell (coord, cell) =
        let next =
            neighbours at coord
            |> List.map snd
            |> step (at coord)
        (coord, next)

    let cells = (world |> Map.toSeq)

    let potential =
        cells
        |> Seq.map (fun (coord, _) -> neighbours at coord)
        |> Seq.concat
        |> Seq.append cells
        |> Seq.distinct
    potential
    |> Seq.map stepCell
    |> Seq.fold (fun w (coord, cell) -> w |> setAt coord cell) emptyWorld

let evolution initial =
    let some x = if x = emptyWorld then None else Some(x, x)
    initial |> Seq.unfold (tick >> some)

// example
let glider =
    [ (2, 0), Alive
      (0, 1), Alive
      (2, 1), Alive
      (1, 2), Alive
      (2, 2), Alive ]

let add pattern (x, y) world =
    let offset (i, j) = x + i, y + j
    pattern
    |> List.fold (fun w (coord, cell) -> w |> setAt (offset coord) cell) world

// draw
let rec drawWorld (area : Rectangle) cellSize world =
    let cells =
        seq {
            for i in [ area.X..area.Right ] do
                for j in [ area.Y..area.Bottom ] -> (i, j)
        }

    let aliveShape (x, y) =
        Rectangle(x * cellSize, y * cellSize, cellSize, cellSize)
    cells
    |> Seq.map (fun coord -> (coord, world |> getAt coord))
    |> Seq.filter (fun (coord, cell) -> cell = Alive)
    |> Seq.fold (fun xs (coord, cell) -> aliveShape coord :: xs) []
    |> drawImg (area.Right * cellSize) (area.Bottom * cellSize)

and drawImg (width : int) height (rects : Rectangle list) =
    let bmp = new Bitmap(width, height)
    use g = Graphics.FromImage(bmp)
    match rects with
    | [] -> ()
    | r -> g.FillRectangles(System.Drawing.Brushes.Red, r |> List.toArray)
    bmp :> Image

/// plaintext format ( http://www.conwaylife.com/wiki/Plaintext )
let decodePlaintext text =
    let data = text |> List.filter (fun (l: string) -> not <| l.StartsWith("!"))
    let cell s = if s = "O" then Alive else Dead
    let row y d = d |> List.mapi (fun x c -> ((x, y), cell c))
    data
    |> List.map (fun line -> line.ToCharArray() |> Seq.map (fun x -> x.ToString()) |> Seq.toList)
    |> List.mapi (fun y t -> t |> row y)
    |> List.concat
    |> List.fold (fun w (coord, c) -> w |> setAt coord c) emptyWorld

let downloadPattern name =
    use client = new System.Net.WebClient()
    let url = sprintf "http://www.conwaylife.com/patterns/%s.cells" name
    client.DownloadString(Uri(url))
    |> (fun x -> x.Split('\n') |> Seq.toList)
    |> decodePlaintext

let glider2 () = downloadPattern <| "glider"
let baker () = downloadPattern <| "baker"
let gosperglidergun () = downloadPattern <| "gosperglidergun"

// let's try
let run times world =
    world
    |> evolution
    |> Seq.zip [ 0..times ]
    |> Seq.map snd

let sample = emptyWorld |> add glider (0, 0)

open LightTable.FSharp.Inspector.Helpers

let gif initial =
    [ initial ]
    |> Seq.append (initial |> run 60)
    |> Seq.map (drawWorld (Rectangle(0, 0, 40, 40)) 25)
    |> Gif.create (TimeSpan.FromSeconds(0.2))


gosperglidergun () |> gif
