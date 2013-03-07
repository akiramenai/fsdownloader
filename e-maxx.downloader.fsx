#r @"d:\DevTools\NetLibs\HtmlAgilityPack.dll"

open System.IO
open System.Net
open System.Text
open System.Text.RegularExpressions
open System.Threading.Tasks
open HtmlAgilityPack

// Unfortunatly, it doesn't work :(
(*let htmlWeb = new HtmlWeb() 
htmlWeb.OverrideEncoding <- Encoding.GetEncoding(1251);
htmlWeb.AutoDetectEncoding <- false
let html = (new HtmlWeb()).Load(@"http://e-maxx.ru/bookz/")*)

let req = WebRequest.Create(@"http://e-maxx.ru/bookz/")
let resp = req.GetResponse()
let sr = new StreamReader(resp.GetResponseStream(), System.Text.Encoding.GetEncoding("windows-1251"));
let html = new HtmlDocument()
html.LoadHtml(sr.ReadToEnd())
let content = html.DocumentNode.SelectSingleNode("//td[@class=\"content\"]")
for hr in content.SelectNodes("//hr") do content.RemoveChild(hr) |> ignore

let (|Match|_|) pattern input =
    let m = Regex.Match(input, pattern) in
    if m.Success then Some (List.tail [ for g in m.Groups -> g.Value ]) else None

let processSubcategory (ulNode : HtmlNode) = 
  [
    for anchor in ulNode.SelectNodes("li/a") do
      yield 
        (
          anchor.GetAttributeValue("href", ""),
          anchor.InnerText
        )
  ]

fsi.CommandLineArgs |> Array.iter (printf "%s ")
printfn ""

let folder, concurLevel = 
  match fsi.CommandLineArgs.Length with
    x when x < 2 -> @".\", 4
    |x when x < 3 -> fsi.CommandLineArgs.[1] + @"\", 4
    |x when x = 3 -> fsi.CommandLineArgs.[1] + @"\", int fsi.CommandLineArgs.[2]
    |_ -> failwith "Wrong number of command line attributes"

let books = 
  [
    for category in content.SelectNodes("//h2") do
      let rec loop acc (subcat : HtmlNode) = 
        if (subcat = null) || (subcat.Name.ToLower() = "h2") then acc
        elif subcat.Name.ToLower() = "h3" then
          loop (subcat::acc) subcat.NextSibling
        else loop acc subcat.NextSibling
      for subcategory in loop [] category.NextSibling do
        for book in processSubcategory subcategory.NextSibling do
          match category.InnerText, subcategory.InnerText with
          x, "" -> yield (x + @"\", fst book, snd book)
          | x, y when x = y -> yield (x + @"\", fst book, snd book)
          | x, y -> yield (x + @"\" + y + @"\", fst book, snd book)      
  ]
  |> List.map (fun x -> 
      let p, (l : string), n = x
      let n' = 
        match n with
          Match @"([\w\s,]*)\.(.*)\s*\(.*\)" result -> result.[0] + "."+ result.[1]
          | _ -> failwith "Matching error"
      let n'' = 
        n'.Replace('(','_').Replace(')', '_').Replace('?', '.').Replace(':', '-') + 
          l.Substring(l.LastIndexOf('.'))
        |> fun y -> y.TrimEnd([|' '|]);
      folder + p + n'', l)

let counter = ref 0
let size = List.length books

let downloadTask task = 
  let client = new WebClient()
  new System.Action(fun () -> 
    fst task 
    |> fun (t:string) -> t.Substring(0, t.LastIndexOf(@"\"))
    |> fun dir -> Directory.CreateDirectory(dir)
    |> ignore
    client.DownloadFile(new System.Uri(snd task), fst task)
    lock(counter) ( fun () ->
      counter := !counter + 1
      printfn "Complite %d of %d" !counter size))

printfn "Downloading to folder : %s" folder
let dTasks = books |> List.map downloadTask |> List.toArray
let parOpts = new ParallelOptions()
parOpts.MaxDegreeOfParallelism <- concurLevel
Parallel.Invoke(parOpts, dTasks)
printfn "Done!"  
System.Console.ReadLine()