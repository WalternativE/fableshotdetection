module App.Components.Analyzer

open System
open Elmish
open Fable.Core
open Fable.Import
open App
open App.Global

type Model = {
    videoId : string
    backingContext : Browser.CanvasRenderingContext2D option
    histContext : Browser.CanvasRenderingContext2D option
    visualizerContext : Browser.CanvasRenderingContext2D option
    isAnalyzing : bool
    videoWidth : float
    videoHeight : float
    lastShotHist : int list
    visBuffer : int list
}

type Msg =
    | StartVideoMsg
    | StopVideoMsg
    | ShotDetectedMsg of string option
    | GlobalMsg of Global.Msg

let init videoId =
    { videoId = videoId
      backingContext = None
      histContext = None
      visualizerContext = None
      isAnalyzing = false
      videoWidth = 0.
      videoHeight = 0.
      lastShotHist = []
      visBuffer = [] }, Cmd.none

let extractDrawingContext id =
    let el = (Browser.document.getElementById id) :?> Browser.HTMLCanvasElement
    el.getContext_2d ()

let getVideoMeasurments id =
    let el = (Browser.document.getElementById id) :?> Browser.HTMLVideoElement
    (el.clientWidth, el.clientHeight)

let drawOnBacking model =
    let vid = (Browser.document.getElementById model.videoId) :?> Browser.HTMLVideoElement
    model.backingContext |> Option.iter (fun ctx -> ctx.drawImage (U3.Case3 vid, 0., 0., model.videoWidth, model.videoHeight) )
    model

let clamp (v : float) min max =
    match v with
    | _ when v < min -> int min
    | _ when v > max -> int max
    | _ -> int (System.Math.Floor (v))

let computeHistogram model =
    let pixels =
        model.backingContext
        |> Option.map (fun ctx -> ctx.getImageData (0., 0., model.videoWidth, model.videoHeight))
        |> Option.map (fun id -> id.data )

    // TODO might be 'prettier' with an active pattern
    // don't know if I manage to make it performant though
    pixels
    |> Option.map ( fun pxs ->
        let mutable hist = Array.zeroCreate 256
        for row in { 0 .. int model.videoHeight - 1 } do
             for column in { 0 .. int model.videoWidth - 1 } do
                let i = (row * int model.videoWidth + column) * 4
                let red = float pxs.[i]
                let green = float pxs.[i + 1]
                let blue = float pxs.[i + 2]
                
                let normalizedLuma = clamp (0.299 * red + 0.587 * green + 0.114 * blue) 0. 255.
                hist.[normalizedLuma] <- hist.[normalizedLuma] + 1
        model, hist )

let drawOnCanvas = function
    | Some (model, hist) ->
        match model.histContext with
        | Some ctx ->
            ctx.clearRect (0., 0., 256., 100.)
            let max = Array.max hist
            ctx.beginPath ()
            for bucket in {0 .. 255} do
                let scalingFactor = 100. / float max
                let v = clamp (float hist.[bucket] * scalingFactor) 0. 100.
                
                ctx.moveTo (float bucket, 100.)
                ctx.lineTo (float bucket, 100. - (float v))
                ctx.stroke ()
            ctx.closePath ()
            Some hist
        | None -> None
    | None -> None

let processFrame = drawOnBacking >> computeHistogram >> drawOnCanvas

let computeTotalLuma (hist : int list) =
    let rec start hist accVal index =
        match hist with
        | [] -> accVal
        | curVal::rest -> start rest (accVal + curVal * index) (index + 1)

    start hist 0 0

let computeDifference oldHist newHist =
    Math.Abs (computeTotalLuma oldHist - computeTotalLuma newHist)

let detectShot model currentHist =
    // TODO implement all the logic
    let difference =
        computeDifference model.lastShotHist currentHist

    if difference > 1658926 then
        let img =
            model.backingContext
            |> Option.map (fun ctx -> (ctx.canvas.toDataURL("image/png")))
        difference, Cmd.ofMsg (ShotDetectedMsg img)
    else
        difference, Cmd.none

let processVisBuffer model difference =
    if List.length model.visBuffer < 256 then
        { model with visBuffer = difference::model.visBuffer }
    else
        let head = model.visBuffer |> List.take 255
        { model with visBuffer = difference::head }

let drawVisBuffer model =
    model.visualizerContext
    |> Option.iter (fun ctx ->
        let scalingFactor = 100. / float TMAX

        let rec draw toDraw pos =
            match toDraw with
            | [] -> ()
            | x::rest ->
                let v = clamp (float x * scalingFactor) 0. 100.
                ctx.moveTo(float pos, 100.)
                ctx.lineTo(float pos, 100. - float v)
                ctx.stroke()
                pos - 1 |> draw rest
        
        ctx.clearRect(0., 0., 256., 100.)
        ctx.beginPath ()
        draw model.visBuffer 255 )
    model

let visualizeDifferences difference = processVisBuffer difference >> drawVisBuffer

let update msg model =
    match msg with
    | StartVideoMsg ->
        Browser.console.log "Starting video"
        let backingContext = extractDrawingContext "canvas-backing"
        let histContext = extractDrawingContext "canvas-hist"
        let visualizerContext = extractDrawingContext "canvas-visualizer"
        let width, height = getVideoMeasurments model.videoId
        { model with
              backingContext = Some backingContext
              histContext = Some histContext
              visualizerContext = Some visualizerContext
              isAnalyzing = true
              videoWidth = width
              videoHeight = height } , Cmd.none
    | StopVideoMsg ->
        Browser.console.log "Stopping video"
        { model with isAnalyzing = false }, Cmd.none
    | ShotDetectedMsg _ ->
        model, Cmd.none
    | GlobalMsg msg ->
        match msg with
        | Tick dt ->
            if model.isAnalyzing then
                let hist = (processFrame model) |> Option.defaultValue [||] |> List.ofArray
                let difference, cmd = hist |> detectShot model
                let modelWithUpdateVisBuffer = visualizeDifferences model difference
                { modelWithUpdateVisBuffer with lastShotHist = hist }, cmd
            else
                model, Cmd.none

module R = Fable.Helpers.React

let view model dispatch =
    R.div [ R.Props.Style [R.Props.Display "inline-block"] ] [
        R.canvas [
                R.Props.Id "canvas-backing"
                R.Props.Width model.videoWidth
                R.Props.Height model.videoHeight
                R.Props.Hidden true ] [
            R.div [] [ R.str "Canvas not supported" ]
        ]
        R.canvas [
            R.Props.Id "canvas-hist"
            R.Props.Width "256px"
            R.Props.Height "100px"
            R.Props.Style [R.Props.MarginLeft "10px"] ] [
            R.div [] [ R.str "Canvas not supported" ]
        ]
        R.canvas [
            R.Props.Id "canvas-visualizer"
            R.Props.Width "256px"
            R.Props.Height "100px"
            R.Props.Style [R.Props.MarginLeft "10px"]
        ] [ R.div [] [ R.str "Canvas not supported"] ]
    ]