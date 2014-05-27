            
#load "Prelude.fs"
open SupV
#load "Process.fs"

open SupV
open SupV.Process
let s = 
    { Path = @"C:\Program Files (x86)\Microsoft F#\v4.0\Fsi.exe"
      Args = ""
      Restart = true }
let x = spawn s 
x.Post Start
x.Post Stop
x.Post Restart
