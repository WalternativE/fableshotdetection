module App.Components.Viewer

open Elmish
open Fable.Import.Browser
open Fable.Import

type Model ={ 
    selected : string 
    options : string list }

type Msg =
    | SelectVideoMsg of string

let init videoOptions =
    { options = videoOptions; selected = List.head videoOptions }, Cmd.none

let update msg model =
    match msg with
    | SelectVideoMsg vidUrl -> model, Cmd.none

module R = Fable.Helpers.React

let videoSelect model (dispatch: Msg -> unit) =
    R.select [
        R.Props.Style [R.Props.Display "block"; R.Props.MarginBottom "10px"]
        R.Props.Value model.selected
        R.Props.OnChange (fun e ->
            // maybe I find a nicer way to solve this
            let selectElem = e.currentTarget :?> HTMLSelectElement
            SelectVideoMsg selectElem.value |> dispatch
        )] [
            yield! model.options
            |> List.map (fun o -> R.option [R.Props.Value o] [ R.str o ])
        ]

let view model (dispatch: Msg -> unit) =
    R.div [] [
        R.h1 [] [R.str "Video viewer"]
        videoSelect model dispatch
        R.video [
            R.Props.Controls true
            R.Props.Src model.selected 
            R.Props.Width "30%" ] []
    ]