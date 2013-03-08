#r @"d:\DevTools\NetLibs\FSPowerPack\bin\FSharp.PowerPack.dll"
#r @"d:\DevTools\NetLibs\FSPowerPack\bin\FSharp.PowerPack.Parallel.Seq.dll"
#r @"d:\DevTools\NetLibs\FSPowerPack\bin\FSharp.PowerPack.Linq.dll"

open Microsoft.FSharp.Collections
open System.Linq
open System.IO
open System.Text
open System.Text.RegularExpressions
open System.Net

let (|Match|_|) pattern input =
    let m = Regex.Match(input, pattern) in
    if m.Success then Some (List.tail [ for g in m.Groups -> g.Value ]) else None

let readLines (filePath:string) = seq {
    use sr = new StreamReader (filePath)
    while not sr.EndOfStream do
        yield sr.ReadLine ()
}

let linesCount (filePath:string) =
  let sr = new StreamReader (filePath)
  let count = ref 0
  while not sr.EndOfStream do
    sr.ReadLine () |> ignore
    count := !count + 1
  sr.Close()
  !count

let withDegreeOfParallelism n (pq:ParallelQuery<_>) = 
  pq.WithDegreeOfParallelism(n)

let tasklistPattern = "(.+)\t(.+)$"

let taskfile, storepath, concurLevel = 
  match fsi.CommandLineArgs.Length with
    x when x < 2 -> @".\downloads.lst", @".\", 4
    |x when x < 3 -> fsi.CommandLineArgs.[1], @".\", 4
    |x when x < 4 -> fsi.CommandLineArgs.[1], fsi.CommandLineArgs.[2], 4
    |x when x = 4 -> fsi.CommandLineArgs.[1], fsi.CommandLineArgs.[2] + @"\", int fsi.CommandLineArgs.[3]
    |_ -> failwith "Wrong number of command line attributes"

let counter = ref 0
let size = linesCount taskfile
let lockobj = new System.Object()
let client = new WebClient()


client.DownloadFile(new System.Uri(@"http://synrc.com/publications/cat/Algebra/Modules/Anderson%20F.W.%2c%20Fuller%20K.R.%20Rings%20and%20categories%20of%20modules.djvu"),
  @"J:\temp.djvu")

readLines taskfile
|> Seq.map (fun x -> 
  match x with
    Match tasklistPattern result -> (result.[0], result.[1])
    |_ -> failwith "Wrong tasklist format.")
|> PSeq.withDegreeOfParallelism concurLevel
|> PSeq.iter (fun x -> 
  let storefile = 
    let t = fst x in 
      storepath + (t.Replace('/', '\\').Replace('(','_').Replace(')', '_').Replace('?', '.').Replace(':', '-'))
  storefile.Substring(0, storefile.LastIndexOf(@"\"))
  |> fun dir -> Directory.CreateDirectory(dir) |> ignore
  //printfn "Downloading %A to %s" (new System.Uri(snd x)) storefile
  client.DownloadFile(new System.Uri(snd x), storefile)
  lock(lockobj) ( fun () ->
    counter := !counter + 1
    printfn "%s downloaded" (fst x)
    printfn "Complite %d of %d" !counter size))