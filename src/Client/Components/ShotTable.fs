module App.Components.ShotTable

open System
open Elmish
open Fable.Import

type Shot = {
  id : string
}

type Model = {
  shots : Shot list
}

type Msg = 
  | AnalyzerMsg of Analyzer.Msg

module R = Fable.Helpers.React

let init () =
  { shots = [] }, Cmd.none

let update msg model =
  match msg with
  | AnalyzerMsg analyzerMsg ->
      match analyzerMsg with
      | Analyzer.ShotDetectedMsg ->
          let shotId = Guid.NewGuid() |> string
          let newShot = { id = shotId }
          Browser.console.log  (sprintf "Shot detected - shot id is %s" shotId)
          { model with shots = newShot::model.shots }, Cmd.none
      | Analyzer.StartVideoMsg | Analyzer.StopVideoMsg | Analyzer.GlobalMsg _ -> model, Cmd.none

let renderShots model =
  R.div [] [
    yield! model.shots
    |> Seq.map (fun shot -> R.div [] [ R.span [] [ R.str shot.id ] ])
  ]

let view model dispatch =
  renderShots model