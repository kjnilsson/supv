namespace SupV

module Process =
    open System
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
        | Read of AsyncReplyChannel<Instance>
        | Sample

    let start exitHandler ({ Spec = spec } as state) = 
        let proc = create spec.Path spec.Args
        proc.Exited.Add exitHandler 
        if proc.Start() then
            { state with 
                Status = Active
                Pid = Some proc.Id }, (Some proc)
        else
            state, None

    let stop state (p : Process) =
        try
            p.EnableRaisingEvents <- false // else it will trap the exit event and restart
            p.Kill()
            p.Dispose()
        with
        | _ -> ()
        { state with 
            Pid = None
            Status = Stopped }

    type ProcessBox =
        { Post : Protocol -> unit
          Get : unit -> Instance option
          Changes : IEvent<Instance> } 

    let spawn (spec: Spec) =
        let ins = Instance.create spec
        let changes = Event<Instance>()

        let agent = MailboxProcessor.Start(fun inbox ->

            let start = start (fun _ -> inbox.Post Restart)

            let rec loop ({ Spec = spec } as state) (proc : Process option) = 
                async {
                    changes.Trigger state
                    let! msg = inbox.Receive()
                    match msg, proc with
                    | Start, None ->
                        let state', proc = start state
                        return! loop state' proc
                    | Start, Some _ ->
                        return! loop state proc
                    | Restart, Some p when spec.Restart ->
                        let state = stop state p
                        do! Async.Sleep 2000
                        let state, proc = start state
                        return! loop state proc
                    | Restart, Some p ->
                        let state' = stop state p
                        return! loop state' None
                    | Stop, Some p ->
                        let state' = stop state p
                        return! loop state' None
                    | Read rc, _ ->
                        rc.Reply state
                        return! loop state proc
                    | _ -> 
                        return! loop state proc }
            loop ins None)

        { Post = agent.Post
          Get = fun () -> agent.TryPostAndReply ((fun rc -> Read rc), 2500)
          Changes = changes.Publish }