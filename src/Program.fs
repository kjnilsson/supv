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
        | Read of AsyncReplyChannel<(Instance) list>
        
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
                    | Read rc ->
                        let data =
                            state.Items
                            |> List.map (fun (s, p) ->
                                p.Get().Value)
                        rc.Reply data
                        return! loop state
                    | _ ->
                        return! loop state }
            loop { Items = [] })
                
open SupV
open FSharp.Data

module Text =
    open System.Collections.Generic
    open Newtonsoft.Json
    open Newtonsoft.Json.Serialization
    open FifteenBelow.Json

    let private converters =
        [ OptionConverter () :> JsonConverter
          TupleConverter () :> JsonConverter
          ListConverter () :> JsonConverter
          MapConverter () :> JsonConverter
          BoxedMapConverter () :> JsonConverter
          UnionConverter () :> JsonConverter ] |> List.toArray :> IList<JsonConverter>

    let private settings =
        JsonSerializerSettings (
            ContractResolver = CamelCasePropertyNamesContractResolver (), 
            Converters = converters,
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore)

    let serialize t =
        JsonConvert.SerializeObject(t, settings)

    let deserialize<'T> s =
        JsonConvert.DeserializeObject<'T>(s, settings)

module Web =
    open Text
    open Suave
    open Suave.Http
    open Suave.Http
    open Suave.Http.Successful
    open Suave.Http.Applicatives
    open Suave.Web

    let allInstances (supv : MailboxProcessor<SupProtocol>) =
        let instances = supv.PostAndReply (fun rc -> Read rc)
        OK (serialize instances)

    let instance (supv : MailboxProcessor<SupProtocol>) (instanceId : Id) =
        let instances = supv.PostAndReply (fun rc -> Read rc)
        let instance = instances |> List.find (fun xx -> xx.Id = instanceId) 
        OK (serialize instance)

    let start (supv : MailboxProcessor<SupProtocol>) =
        let app = 
            choose [
                GET >>= url "/instances" >>== (fun req -> allInstances supv)
                GET >>= url_scan "/instances/%s/restart" (fun i -> OK "restarted")
                GET >>= url_scan "/instances/%s" (fun i -> instance supv (Id i))
            ]
        web_server default_config app 

[<EntryPoint>]
let main argv = 
    let config = JsonValue.Load "config.json"

    let specs =
        config.AsArray()
        |> Array.map (fun jv ->
            match jv with
            | JsonValue.Record data ->
                let (JsonValue.String path) = data |> Array.find (fst >> (=) "path") |> snd
                let (JsonValue.String args) = data |> Array.find (fst >> (=) "args") |> snd
                let (JsonValue.Boolean restart) = data |> Array.find (fst >> (=) "restart") |> snd
                { Path = path; Args = args; Restart = restart }
            | _ -> failwith "invalid json")


    let supv = spawn ()
    specs |> Array.iter (fun spec -> supv.Post (Add spec))

    Web.start supv

    0 // return an integer exit code