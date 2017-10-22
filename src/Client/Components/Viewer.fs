module App.Components.Viewer

open Elmish
open Fable.Import.Browser

type Model ={ 
    selected : string 
    options : string list
    analyzer : Analyzer.Model }

type Msg =
    | SelectVideoMsg of string
    | AnalyzerMsg of Analyzer.Msg

let init videoOptions =
    let analyzer, analyzerCmd = Analyzer.init "player"
    { options = videoOptions
      selected = List.head videoOptions
      analyzer = analyzer }, analyzerCmd

let update msg model =
    match msg with
    | SelectVideoMsg vidUrl -> { model with selected = vidUrl }, Cmd.none
    | AnalyzerMsg msg ->
        match msg with
        | Analyzer.StartVideoMsg | Analyzer.StopVideoMsg | Analyzer.UpdateFrameMsg ->
            let aMod, aCmd = Analyzer.update msg model.analyzer
            { model with analyzer = aMod }, aCmd |> Cmd.map AnalyzerMsg

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
            R.Props.Id "player"
            R.Props.Controls true
            R.Props.Src model.selected 
            R.Props.Width "30%"
            R.Props.OnPlay (fun _ -> AnalyzerMsg Analyzer.StartVideoMsg |> dispatch)
            R.Props.OnPause (fun _ -> AnalyzerMsg Analyzer.StopVideoMsg |> dispatch) ] []
        Analyzer.view model.analyzer dispatch
    ]