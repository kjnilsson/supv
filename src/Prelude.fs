namespace SupV

open System

[<AutoOpen>]
module Prelude =

    type Guid with
        static member short () =
            (Guid.NewGuid().ToString()).Substring(0, 8)


[<AutoOpen>]
module Data =

    type Id = | Id of string

    type Spec =
        { Path : string
          Args : string
          Restart : bool }

    type Status = 
        | Active
        | Stopped

    type Instance =
        { Id : Id
          Spec : Spec
          Status : Status
          Pid : int option }

        static member create spec =
            { Id = Id (Guid.short())
              Spec = spec
              Status = Stopped
              Pid = None }
              
            
            