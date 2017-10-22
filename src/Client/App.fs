module App.View

open Elmish
open Elmish.React.Common
open Fable.Core
open Fable.Core.JsInterop
open App.Components

importSideEffects "whatwg-fetch"
importSideEffects "babel-polyfill"

importAll "./sass/main.sass"

//hardcoded until better idea
let videoChoices = [
  "/videos/It2017.mp4"
  "/videos/bigbuckbunny.mp4"
  "/videos/trailer_hd.mp4" ]

type Msg =
  | InitMsg
  | ViewerMsg of Viewer.Msg

type Model = {
    viewer : Viewer.Model
  }

let init result =
  let viewer, viewerCmd = Viewer.init videoChoices
  { viewer = viewer }, Cmd.batch [Cmd.ofMsg InitMsg; viewerCmd]

let update msg model =
  match msg with
  | InitMsg -> model, Cmd.none
  | ViewerMsg msg ->
      match msg with
      | Viewer.SelectVideoMsg videoUrl ->
          let viewer, viewerCmd = Viewer.update msg { model.viewer with selected = videoUrl }
          { model with viewer = viewer }, viewerCmd |> Cmd.map ViewerMsg
      | Viewer.AnalyzerMsg _ ->
          let v, vCmd = Viewer.update msg model.viewer
          { model with viewer = v }, vCmd |> Cmd.map ViewerMsg

module R = Fable.Helpers.React

let view model dispatch =
  R.div [] [
    lazyView2 Viewer.view model.viewer (ViewerMsg >> dispatch)
  ]

open Elmish.React
open Elmish.Debug
open Elmish.HMR

// App
Program.mkProgram init update view
#if DEBUG
|> Program.withDebugger
|> Program.withHMR
#endif
|> Program.withReact "elmish-app"
|> Program.run
