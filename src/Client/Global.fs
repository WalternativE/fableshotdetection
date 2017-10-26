module App.Global

open System

[<Literal>]
let TMAX = 8355840

type Msg =
    | Tick of DateTime