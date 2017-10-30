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
    visWidth : float
    visHeight : float
    lastFadeThreshDelta : int
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

let init videoId visDimensions =
    let visWidth, visHeight = defaultArg visDimensions (256., 100.)

    { videoId = videoId
      backingContext = None
      histContext = None
      visualizerContext = None
      isAnalyzing = false
      videoWidth = 0.
      videoHeight = 0.
      lastShotHist = []
      visBuffer = []
      cutThresh = Math.Floor (TMAX / 2.5) |> int
      fadeThresh = Math.Floor (TMAX / 8.) |> int
      visWidth = visWidth
      visHeight = visHeight
      lastFadeThreshDelta = 0 }, Cmd.none

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
            ctx.clearRect (0., 0., model.visWidth, model.visHeight)
            let max = Array.max hist
            ctx.beginPath ()
            for bucket in {0 .. 255} do
                let scalingFactor = model.visHeight / float max
                let v = clamp (float hist.[bucket] * scalingFactor) 0. model.visHeight
                
                ctx.moveTo (float bucket, model.visHeight)
                ctx.lineTo (float bucket, model.visHeight - (float v))
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
    let unclamped = Math.Abs (computeTotalLuma oldHist - computeTotalLuma newHist)
    clamp (float unclamped) 0. TMAX

let extractImage (ctx : Browser.CanvasRenderingContext2D option) =
    ctx
    |> Option.map (fun ctx -> (ctx.canvas.toDataURL("image/png")))

let detectShot model currentHist =
    let difference =
        computeDifference model.lastShotHist currentHist

    if difference > model.cutThresh then
        let img = extractImage model.backingContext
        difference, Cmd.ofMsg (ShotDetectedMsg img), model
    else if difference > model.fadeThresh then
        let currentFadeThreshDelta = model.lastFadeThreshDelta + (difference - model.fadeThresh)
        let adjustedModel = { model with lastFadeThreshDelta = currentFadeThreshDelta }

        if currentFadeThreshDelta > model.cutThresh then
            let img = extractImage model.backingContext
            difference, Cmd.ofMsg (ShotDetectedMsg img), adjustedModel
        else
            difference, Cmd.none, adjustedModel
    else
        // a fade should be continuous - if it is not reset the delta to zero
        difference, Cmd.none, { model with lastFadeThreshDelta = 0 }

let processVisBuffer model difference =
    if List.length model.visBuffer < (int model.visWidth) then
        { model with visBuffer = difference::model.visBuffer }
    else
        let head = model.visBuffer |> List.take (int model.visWidth - 1)
        { model with visBuffer = difference::head }

let drawVisBuffer model =
    model.visualizerContext
    |> Option.iter (fun ctx ->
        let scalingFactor = model.visHeight / TMAX

        let rec draw toDraw pos =
            match toDraw with
            | [] -> ()
            | x::rest ->
                let v = clamp (float x * scalingFactor) 0. model.visHeight
                ctx.moveTo(float pos, model.visHeight)
                ctx.lineTo(float pos, model.visHeight - float v)
                ctx.stroke()
                pos - 1 |> draw rest
        
        ctx.clearRect(0., 0., model.visWidth, model.visHeight)
        ctx.beginPath ()
        draw model.visBuffer (int model.visWidth - 1)

        let cutThreshPos = clamp (float model.cutThresh * scalingFactor) 0. model.visHeight
        ctx.moveTo(0., model.visHeight - float cutThreshPos)
        ctx.lineTo(model.visWidth, model.visHeight - float cutThreshPos)
        ctx.stroke()

        let fadeThreshPos = clamp (float model.fadeThresh * scalingFactor) 0. model.visHeight
        ctx.moveTo(0., model.visHeight - float fadeThreshPos)
        ctx.lineTo(model.visWidth, model.visHeight - float fadeThreshPos)
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
        let modelWithResetFadeDelta = { model with lastFadeThreshDelta = 0 }
        modelWithResetFadeDelta, Cmd.none
    | ThreshUpdatedMsg thresh ->
        match thresh with
        | CutThresh v -> { model with cutThresh = v }, Cmd.none
        | FadeThresh v -> { model with fadeThresh = v }, Cmd.none
    | GlobalMsg msg ->
        match msg with
        | Tick _ ->
            if model.isAnalyzing then
                // TODO if I ever have time - this block is rather ugly from a functional perspective an I should refactor it
                let hist = (processFrame model) |> Option.defaultValue [||] |> List.ofArray
                let difference, cmd, modelWithNewFadeThreshDelta = hist |> detectShot model
                let modelWithUpdateVisBuffer = visualizeDifferences modelWithNewFadeThreshDelta difference
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
        R.div [ R.Props.Style [ R.Props.MarginLeft "10px" ] ] [
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
            R.div [] [ R.span [] [R.str (sprintf "Accumulated fade threshold delta: %i" model.lastFadeThreshDelta)] ]
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
                R.Props.Width "256px" // I really don't want to support anything else than line histograms now
                R.Props.Height model.visHeight
                R.Props.Style [R.Props.MarginLeft "10px"]
            ] [ R.div [] [ R.str "Canvas not supported" ] ]
            R.canvas [
                R.Props.Id "canvas-visualizer"
                R.Props.Width model.visWidth
                R.Props.Height model.visHeight
                R.Props.Style [R.Props.MarginLeft "10px"]
            ] [ R.div [] [ R.str "Canvas not supported"] ]
        ]
    ]