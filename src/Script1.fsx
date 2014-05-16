            
#load "Prelude.fs"
open SupV
#load "Process.fs"

open SupV
open SupV.Process

let s = 
    { Path = @"d:\zorrilloservices\VersionedStorage\Zorrillo.Infrastructure.VersionedStorage.Service.exe"
      Args = ""
      Restart = true }
let x = spawn s 
x.Post Start
x.Post Stop
x.Post Restart
x.PostAndReply (fun rc -> Read rc)
