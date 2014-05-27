open System 
open SupV

module SupV =
    open Process

    type Item = Spec * ProcessBox
    type SupState =
        { Items : Item list }

    type SupProtocol =
        | Add of Spec
        | Remove of Spec
        
    let spawn () =
        MailboxProcessor.Start(fun inbox ->
            let rec loop state =
                async {
                    let! msg = inbox.Receive()
                    match msg with
                    | Add spec ->
                        let p = Process.spawn spec
                        p.Post Start
                        return! loop { state with Items = (spec, p) :: state.Items }
                    | _ ->
                        return! loop state }
            loop { Items = [] })
                
open SupV
open FSharp.Data

[<EntryPoint>]
let main argv = 
    let config = JsonValue.Load "config.json"

    let specs =
        config.AsArray()
        |> Array.map (fun jv ->
            match jv with
            | JsonValue.Record data ->
                let (JsonValue.String path) = data |> Array.find (fun (k, _) -> k = "path") |> snd
                let (JsonValue.String args) = data |> Array.find (fun (k, _) -> k = "args") |> snd
                let (JsonValue.Boolean restart) = data |> Array.find (fun (k, _) -> k = "restart") |> snd
                { Path = path; Args = args; Restart = restart }
            | _ -> failwith "invalid json")

    let supv = spawn ()
    specs |> Array.iter (fun spec -> supv.Post (Add spec))

    Console.ReadLine() |> ignore

    0 // return an integer exit code