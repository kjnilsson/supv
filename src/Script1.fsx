#load "Prelude.fs"

open System
open SupV
Id (Guid.short())

open System.IO
open System.Diagnostics

let create name args =
    let proc = new Process()
    proc.StartInfo.UseShellExecute <- false
    proc.StartInfo.FileName <- name 
    proc.StartInfo.CreateNoWindow <- true
    proc.StartInfo.Arguments <- args 
    proc.StartInfo.RedirectStandardOutput <- true
    proc.OutputDataReceived.Add (fun args -> printfn "%s" args.Data)
    proc.EnableRaisingEvents <- true
    proc


type Protocol =
    | Start
    | Restart
    | Stop
    | Sample

let spawn (spec: Spec) =
    let ins = Instance.create spec
    
    let start spec handleExit =
        let p = create spec.Path spec.Args
        p.Exited.Add handleExit 
        if p.Start() then
            Some p
        else 
            p.Dispose()
            None

    let agent = MailboxProcessor.Start(fun inbox ->

        let handleExit = (fun _ -> inbox.Post Restart)

        let rec loop state proc = async {
            printfn "State: %A %A" state proc
            let! msg = inbox.Receive()
            printfn "msg: %A" msg
            match msg with
            | Start ->
                match start state.Spec handleExit with
                | Some proc ->
                    return! loop { state with Status = Active; Pid = Some proc.Id } (Some proc)
                | None ->
                    inbox.Post Restart
                    return! loop state proc
            | Restart when state.Spec.Restart ->
                do! Async.Sleep 2000
                inbox.Post Start
                return! loop { state with Status = Stopped } proc
            | Stop ->
//            | Restart when not state.Spec.Restart ->
                printfn "Stop"
                let state =
                    match proc with
                    | Some p ->
                        printfn "killing... "
                        p.EnableRaisingEvents <- false
                        p.Kill()
                        { state with Pid = None }
                    | None -> state
                return! loop { state with Status = Stopped } proc
            | _ -> return! loop state proc }
        loop ins None)
    agent

        
    
let s = 
    { Path = @"C:\zorrilloservices\VersionedStorage\Zorrillo.Infrastructure.VersionedStorage.Service.exe"
      Args = ""
      Restart = true }
let x = spawn s 
x.Post Start
x.Post Stop