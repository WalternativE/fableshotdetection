module App.Components.ShotTable

open System
open Elmish
open Fable.Import

type Shot = {
  id : string
  imageUri : string
}

type Model = {
  videoId : string
  shots : Shot list
}

type Msg = 
  | AnalyzerMsg of Analyzer.Msg

module R = Fable.Helpers.React

let init videoId =
  { videoId = videoId; shots = [] }, Cmd.none

let createShotThumb model currentShot =
  ()

let update msg model =
  match msg with
  | AnalyzerMsg analyzerMsg ->
      match analyzerMsg with
      | Analyzer.ShotDetectedMsg imageUri ->
          let shotId = Guid.NewGuid() |> string
          let newShot = { id = shotId; imageUri = imageUri |> Option.defaultValue "" }
          Browser.console.log  (sprintf "Shot detected - shot id is %s" shotId)
          { model with shots = newShot::model.shots }, Cmd.none
      | Analyzer.StartVideoMsg | Analyzer.StopVideoMsg | Analyzer.GlobalMsg _ -> model, Cmd.none

let renderShots model =
  R.div [] [
    yield! model.shots
    |> Seq.map (fun shot ->
          R.img [ R.Props.Id shot.id; R.Props.Src shot.imageUri ] )
  ]

let view model dispatch =
  renderShots model