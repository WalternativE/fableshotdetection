module App.Components.Analyzer

open Elmish
open Fable.Core
open Fable.Import
open App
open App.Global

type Model = {
    videoId : string
    context : Browser.CanvasRenderingContext2D option
    isAnalyzing : bool
    videoWidth : float
    videoHeight : float
}

type Msg =
    | StartVideoMsg
    | StopVideoMsg
    | GlobalMsg of Global.Msg

let init videoId =
    { videoId = videoId
      context = None
      isAnalyzing = false
      videoWidth = 0.
      videoHeight = 0. }, Cmd.none

let extractDrawingContext id =
    let el = (Browser.document.getElementById id) :?> Browser.HTMLCanvasElement
    el.getContext_2d ()

let getVideoMeasurments id =
    let el = (Browser.document.getElementById id) :?> Browser.HTMLVideoElement
    (el.clientWidth, el.clientHeight)

let drawOnBacking model =
    let vid = (Browser.document.getElementById model.videoId) :?> Browser.HTMLVideoElement
    model.context |> Option.iter (fun ctx -> ctx.drawImage (U3.Case3 vid, 0. , 0., model.videoWidth, model.videoHeight) )

let update msg model =
    match msg with
    | StartVideoMsg ->
        Browser.console.log "Starting video"
        let context = extractDrawingContext "canvas-backing"
        let width, height = getVideoMeasurments model.videoId
        { model with
              context = Some context
              isAnalyzing = true
              videoWidth = width
              videoHeight = height } , Cmd.none
    | StopVideoMsg ->
        Browser.console.log "Stopping video"
        { model with isAnalyzing = false }, Cmd.none
    | GlobalMsg msg ->
        match msg with
        | Tick dt ->
            if model.isAnalyzing then
                drawOnBacking model
                model, Cmd.none
            else
                model, Cmd.none

module R = Fable.Helpers.React

let view model dispatch =
    R.div [ R.Props.Style [R.Props.Display "inline-block"] ] [
        R.canvas [
                R.Props.Id "canvas-backing"
                R.Props.Width model.videoWidth
                R.Props.Height model.videoHeight ] [
            R.div [] [ R.str "Canvas not supported" ]
        ]
        R.canvas [ R.Props.Id "canvas-hist" ] [
            R.div [] [ R.str "Canvas not supported" ]
        ]
    ]