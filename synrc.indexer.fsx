#r @"d:\DevTools\NetLibs\HtmlAgilityPack.dll"
#r @"d:\DevTools\NetLibs\FSPowerPack\bin\FSharp.PowerPack.dll"
#r @"d:\DevTools\NetLibs\FSPowerPack\bin\FSharp.PowerPack.Parallel.Seq.dll"
#r @"d:\DevTools\NetLibs\FSPowerPack\bin\FSharp.PowerPack.Linq.dll"

open System.IO
open System.Text
open System.Text.RegularExpressions
open System.Threading.Tasks
open HtmlAgilityPack
open Microsoft.FSharp.Collections
open System.Linq

let folder, concurLevel = 
  match fsi.CommandLineArgs.Length with
    x when x < 2 -> @".\", 4
    |x when x < 3 -> fsi.CommandLineArgs.[1] + @"\", 4
    |x when x = 3 -> fsi.CommandLineArgs.[1] + @"\", int fsi.CommandLineArgs.[2]
    |_ -> failwith "Wrong number of command line attributes"

let (|Match|_|) pattern input =
    let m = Regex.Match(input, pattern) in
    if m.Success then Some (List.tail [ for g in m.Groups -> g.Value ]) else None

let htmlWeb = new HtmlWeb() 

let don'tDownload = ["Book/"; "Functional Languages/Coq/"; "Systems/"]

let withDegreeOfParallelism n (pq:ParallelQuery<_>) = 
  pq.WithDegreeOfParallelism(n)

let prnLock = new System.Object()

let rec getBooks path pathLink acc = 
  lock(prnLock) (fun () -> printfn "Processing path: %s" path)
  if List.exists ((=) path) don'tDownload then acc
  else
    let html = (new HtmlWeb()).Load(@"http://synrc.com/publications/cat/" + path)
    let anchors = html.DocumentNode.SelectNodes("//a")
    if anchors = null then acc
    else
      let levelLinks, nextLevel = 
        [
          for anchor in html.DocumentNode.SelectNodes("//a") ->
            (System.Web.HttpUtility.UrlDecode(anchor.GetAttributeValue("href","")),
             anchor.GetAttributeValue("href",""))
        ]
        |> List.filter (fst >> (<>) "../")
        |> List.partition (fst >> fun (x:string) -> 
          x.EndsWith("/") |> not)
      let (nextLevelLinks: (string*string) list) =
        nextLevel 
        |> PSeq.map (fun x -> getBooks (path + fst x) (path + snd x) [])
        |> PSeq.withDegreeOfParallelism concurLevel
        |> PSeq.toList
//        |> List.map (fun x -> getBooks (path + fst x) (path + snd x) [])
        |> List.concat

      lock(prnLock) (fun () -> printfn "Processed path: %s" path)
      levelLinks
      |> List.map (fun x -> (path + (fst x), path + (snd x)))
      |> List.append nextLevelLinks

let books = 
  getBooks "" "" []

let outFile = new StreamWriter(folder + "/download.lst")

books 
|> List.iter (fun x -> 
  outFile.WriteLine(sprintf "%s\t%s" (fst x) (@"http://synrc.com/publications/cat/" + (snd x))))

outFile.Close()

printfn "Done!"
System.Console.ReadLine()