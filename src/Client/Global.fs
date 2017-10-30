module App.Global

open System

let TMAX =
    float 8355840 * (2. / 3.) // the upper third of the total luma differences tends to give no extra information

type Msg =
    | Tick of DateTime