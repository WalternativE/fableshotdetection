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
    cutThresh : int
    fadeThresh : int
}

type Thresh =
    | CutThresh of int
    | FadeThresh of int

type Msg =
    | StartVideoMsg
    | StopVideoMsg
    | ShotDetectedMsg of string option
    | ThreshUpdatedMsg of Thresh
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
      visBuffer = []
      cutThresh = Math.Floor (float TMAX / 3.) |> int
      fadeThresh = Math.Floor (float TMAX / 6.) |> int }, Cmd.none

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
    // TODO implement fading logic
    let difference =
        computeDifference model.lastShotHist currentHist

    if difference > model.cutThresh then
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
        draw model.visBuffer 255

        let cutThreshPos = clamp (float model.cutThresh * scalingFactor) 0. 100.
        ctx.moveTo(0., 100. - float cutThreshPos)
        ctx.lineTo(255., 100. - float cutThreshPos)
        ctx.stroke()

        let fadeThreshPos = clamp (float model.fadeThresh * scalingFactor) 0. 100.
        ctx.moveTo(0., 100. - float fadeThreshPos)
        ctx.lineTo(255., 100. - float fadeThreshPos)
        ctx.stroke()

        ctx.closePath () )
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
    | ThreshUpdatedMsg thresh ->
        match thresh with
        | CutThresh v -> { model with cutThresh = v }, Cmd.none
        | FadeThresh v -> { model with fadeThresh = v }, Cmd.none
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

let extractSliderEvtenValue (e : React.FormEvent) =
    let threshSlider = e.currentTarget :?> Browser.HTMLInputElement
    threshSlider.value |> Int32.TryParse

let onCutThreshChange e dispatch =
    let isSuccess, value = extractSliderEvtenValue e
    if isSuccess then ThreshUpdatedMsg (CutThresh value) |> dispatch else ()

let onFadeThreshChange e dispatch =
    let isSuccess, value = extractSliderEvtenValue e
    if isSuccess then ThreshUpdatedMsg (FadeThresh value) |> dispatch else ()

module R = Fable.Helpers.React

let view model dispatch =
    R.div [ R.Props.Style [R.Props.Display "inline-block"] ] [
        R.div [] [
            R.label [ R.Props.HtmlFor "cut-thresh-slider" ] [ R.str (sprintf "CutThresh at %i" model.cutThresh) ]
            R.input [
                R.Props.Id "cut-thresh-slider"
                R.Props.Type "range"
                R.Props.Min 1
                R.Props.Max TMAX
                R.Props.Value (string model.cutThresh)
                R.Props.OnChange (fun e -> onCutThreshChange e dispatch )
            ]
            R.label [ R.Props.HtmlFor "fade-thresh-slider" ] [ R.str (sprintf "FadeThresh at %i" model.fadeThresh) ]
            R.input [
                R.Props.Id "fade-thresh-slider"
                R.Props.Type "range"
                R.Props.Min 1
                R.Props.Max TMAX
                R.Props.Value (string model.fadeThresh)
                R.Props.OnChange (fun e -> onFadeThreshChange e dispatch )
            ]
        ]
        R.div [] [
            R.canvas [
                R.Props.Id "canvas-backing"
                R.Props.Width model.videoWidth
                R.Props.Height model.videoHeight
                R.Props.Hidden true 
            ] [ R.div [] [ R.str "Canvas not supported" ] ]
            R.canvas [
                R.Props.Id "canvas-hist"
                R.Props.Width "256px"
                R.Props.Height "100px"
                R.Props.Style [R.Props.MarginLeft "10px"]
            ] [ R.div [] [ R.str "Canvas not supported" ] ]
            R.canvas [
                R.Props.Id "canvas-visualizer"
                R.Props.Width "256px"
                R.Props.Height "100px"
                R.Props.Style [R.Props.MarginLeft "10px"]
            ] [ R.div [] [ R.str "Canvas not supported"] ]
        ]
    ]