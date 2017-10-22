module App.Components.Analyzer

open Elmish
open Fable.Import.Browser

type Model = {
    videoId : string
    context : CanvasRenderingContext2D option
    isAnalyzing : bool
    lastAnimationTs : int64
}

type Msg =
    | StartVideoMsg
    | StopVideoMsg
    | UpdateFrameMsg

let extractDrawingContext id =
    let el = (document.getElementById id) :?> HTMLCanvasElement
    el.getContext_2d ()

let init videoId =
    { videoId = videoId; context = None; isAnalyzing = false; lastAnimationTs = 0L }, Cmd.none

let update msg model =
    match msg with
    | StartVideoMsg ->
        console.log "Starting video"
        let context = extractDrawingContext "canvas-backing"
        { model with context = Some context; isAnalyzing = true } , Cmd.ofMsg UpdateFrameMsg
    | StopVideoMsg ->
        console.log "Stop video"
        { model with isAnalyzing = false }, Cmd.none
    | UpdateFrameMsg ->
        if model.isAnalyzing then
            let now = System.DateTime.Now.Ticks
            // 10000 ticks in one millisecond
            if now > (model.lastAnimationTs + 10_000L * 1000L) then
                console.log (sprintf "TS is now %i" now)
                { model with lastAnimationTs = now }, Cmd.ofMsg UpdateFrameMsg
            else
                model, Cmd.ofMsg UpdateFrameMsg
        else
            model, Cmd.none

module R = Fable.Helpers.React

let view model dispatch =
    R.div [] [
        R.canvas [ R.Props.Id "canvas-backing" ] [
            R.div [] [ R.str "Canvas not supported" ]
        ]
        R.canvas [ R.Props.Id "canvas-hist" ] [
            R.div [] [ R.str "Canvas not supported" ]
        ]
    ]