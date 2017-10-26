module App.Components.ShotTable

open System
open Elmish

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

let init videoId =
  { videoId = videoId; shots = [] }, Cmd.none

let update msg model =
  match msg with
  | AnalyzerMsg analyzerMsg ->
      match analyzerMsg with
      | Analyzer.ShotDetectedMsg imageUri ->
          let shotId = Guid.NewGuid() |> string
          let newShot = { id = shotId; imageUri = imageUri |> Option.defaultValue "" }
          { model with shots = newShot::model.shots }, Cmd.none
      | Analyzer.StartVideoMsg | Analyzer.StopVideoMsg | Analyzer.GlobalMsg _ -> model, Cmd.none

module R = Fable.Helpers.React

let view model dispatch =
  R.div [] [
    yield! model.shots
    |> Seq.map (fun shot ->
          R.img [ R.Props.Id shot.id; R.Props.Src shot.imageUri ] )
  ]