module App.View

open Elmish
open Elmish.Browser.Navigation
open Elmish.Browser.UrlParser
open Elmish.React.Common
open Fable.Core
open Fable.Core.JsInterop
open Fable.Import
open Fable.Import.Browser
open App.Components

JsInterop.importSideEffects "whatwg-fetch"
JsInterop.importSideEffects "babel-polyfill"

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
  let viewer, viewerCmd = Components.Viewer.init videoChoices
  { viewer = viewer }, Cmd.batch [Cmd.ofMsg InitMsg; viewerCmd]

let update msg model =
  match msg with
  | InitMsg -> model, Cmd.none
  | ViewerMsg msg ->
      match msg with
      | Viewer.SelectVideoMsg videoUrl ->
        let (viewer, viewerCmd) = Viewer.update msg { model.viewer with selected = videoUrl }
        {model with viewer = viewer}, viewerCmd

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
