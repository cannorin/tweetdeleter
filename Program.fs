open System
open System.Linq
open System.IO
open System.Text
open System.Text.RegularExpressions
open System.Runtime.Serialization
open System.Xml
open System.Threading
open System.Threading.Tasks
open CoreTweet
open CoreTweet.Core
open FSharp.Interop.Dynamic
open DynamicJson
open System.Globalization

module Main = 
  begin
    let getTokens () =
      let unix = (Environment.OSVersion.Platform = PlatformID.Unix || Environment.OSVersion.Platform = PlatformID.MacOSX)
      let tf = if unix then ".twtokens" else "twtokens.xml"
      let home = if unix then Environment.GetEnvironmentVariable ("HOME") else Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData), "csharp")
      let tpath = Path.Combine (home, tf)
      Directory.CreateDirectory home |> ignore
      let x = new DataContractSerializer (typeof<string[]>)
  
      if (File.Exists tpath) then
        use y = XmlReader.Create tpath in
          let ss = x.ReadObject y :?> string [] in
          Tokens.Create(ss.[0], ss.[1], ss.[2], ss.[3])
      else
        Console.Write "Consumer key> "
        let ck = Console.ReadLine ()
        Console.Write "Consumer secret> "
        let cs = Console.ReadLine ()
        let se = OAuth.Authorize (ck, cs) in
        Console.WriteLine ("Open: " + se.AuthorizeUri.ToString ())
        Console.Write "PIN> "
        let g = se.GetTokens (Console.ReadLine()) in
        let s = XmlWriterSettings () in
        s.Encoding <- System.Text.UTF8Encoding false
        use y = XmlWriter.Create (tpath, s) in
            x.WriteObject (y, [| g.ConsumerKey; g.ConsumerSecret; g.AccessToken; g.AccessTokenSecret |])
        g

    [<EntryPoint>]
    let main argv = 
      if (argv.Length = 0) then
        printfn "tweetdeleter: no input. please specify yyyy_MM.js file(s) found in your tweets.zip .";
        Environment.Exit -1;

      let t = getTokens () in
      let ts = Seq.map File.ReadAllLines argv
                   |> Seq.map (Seq.skip 1)
                   |> Seq.map (fun xs -> String.Join("", xs)) 
                   |> Seq.map (fun x -> 
                      let j = (JsonArray.Parse x).Values in
                      Seq.map (fun y -> 
                        (
                          (string y?id_str).Replace("\"", "") |> Int64.Parse,
                          (y?Has ("retweeted_status") : bool),
                          DateTime.ParseExact((string y?created_at).Replace("\"", ""), "yyyy-MM-dd HH:mm:ss +0000", CultureInfo.InvariantCulture)
                        )
                      ) j
                   )
                   |> Seq.concat in
      
      let dts =
        printf "Do you speficy the period of tweet you want to delete? [y/N] ";
        if Console.ReadLine().ToLower() = "y" then
          let fmt = "yyyy/MM/dd HH:mm:ss" in
          printfn "Input start date in UTC +0. [%s]" fmt; printf "> ";
          let sd = DateTime.ParseExact(Console.ReadLine(), fmt, CultureInfo.InvariantCulture) in
          printfn "Input finish date in UTC +0. [%s]" fmt; printf "> ";
          let fd = DateTime.ParseExact(Console.ReadLine(), fmt, CultureInfo.InvariantCulture) in
          Seq.filter (fun (_, _, d) -> sd < d && d < fd) ts
        else ts in
      
      printfn "Reading json(s)..."
      let l = Seq.length dts in
      printf "Do you want to delete these %i tweets? [Y/n] " l;
      let errs = new System.Collections.Generic.List<int64>() in
      if Console.ReadLine().ToLower() <> "n" then
          let c = ref 1 in
          for (i, b, _) in dts do
            let rec loop () =
              let cc = !c in
              try
                if b then
                  t.Statuses.Unretweet(id = i) |> ignore;
                else
                  t.Statuses.Destroy(id = i) |> ignore;
                printfn "Deleted: %i/%i" cc l
              with
                | :? TwitterException as e -> 
                  if e.Status = Net.HttpStatusCode.NotFound then
                    printfn "This tweet is already deleted(%i/%i)." cc l
                  else
                    printfn "Error:   %i/%i (%s)" cc l e.Message;
                    errs.Add i
                | e -> raise e
                in
            loop() 
            c := !c + 1
            Thread.Sleep 250

      if errs.Any() then
        let printerr (x : string) = System.Console.Error.WriteLine x in
        sprintf "Couldn't delete these %i tweets." errs.Count |> printerr
        Seq.iter (sprintf "%i" >> printerr) errs
        1
      else
        printfn "Done."
        0

  end


