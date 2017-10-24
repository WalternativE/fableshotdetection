module App.View

open System
open Elmish
open Elmish.React.Common
open Fable.Core
open Fable.Core.JsInterop
open Fable.Import.Browser
open App.Components
open App.Global

importSideEffects "whatwg-fetch"
importSideEffects "babel-polyfill"

importAll "./sass/main.sass"

//hardcoded until better idea
let videoChoices = [
  "/videos/bigbuckbunny.mp4"
  "/videos/It2017.mp4"
  "/videos/trailer_hd.mp4" ]

type Msg =
  | InitMsg
  | ViewerMsg of Viewer.Msg
  | GlobalMsg of Global.Msg

type Model = {
    viewer : Viewer.Model
  }

// as we have timing problems in this app we need a global clock
let ticker dispatch =
    let msg = GlobalMsg (Tick DateTime.Now)

    window.setInterval(
        (fun _ -> dispatch msg)
        , 1000 / 24 ) |> ignore // divison for roughly 24 fps

let subscription _ = Cmd.ofSub ticker

let init result =
  let viewer, viewerCmd = Viewer.init videoChoices
  { viewer = viewer }, Cmd.batch [Cmd.ofMsg InitMsg; viewerCmd]

let update msg model =
  match msg with
  | InitMsg -> model, Cmd.none
  | GlobalMsg msg ->
      match msg with
      | Tick dt ->
          let msg = Viewer.GlobalMsg (Tick dt)
          let v, vCmd = Viewer.update msg model.viewer
          { model with viewer = v}, vCmd |> Cmd.map ViewerMsg
  | ViewerMsg msg ->
      match msg with
      | Viewer.SelectVideoMsg videoUrl ->
          let viewer, viewerCmd = Viewer.update msg { model.viewer with selected = videoUrl }
          { model with viewer = viewer }, viewerCmd |> Cmd.map ViewerMsg
      | Viewer.AnalyzerMsg _ ->
          let v, vCmd = Viewer.update msg model.viewer
          { model with viewer = v }, vCmd |> Cmd.map ViewerMsg
      | Viewer.GlobalMsg _ -> model, Cmd.none // I already handle this once - no state change

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
|> Program.withSubscription subscription
#if DEBUG
|> Program.withDebugger
|> Program.withHMR
#endif
|> Program.withReact "elmish-app"
|> Program.run
