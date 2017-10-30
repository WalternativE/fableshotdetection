// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

#r @"packages/build/FAKE/tools/FakeLib.dll"
#r @"packages/build/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"

open Fake
open System
open System.IO

let clientPath = "./src/Client" |> FullName

let serverPath = "./src/Server/" |> FullName

open Newtonsoft.Json.Linq

let dotnetcliVersion : string = 
    try
        let content = File.ReadAllText "global.json"
        let json = Newtonsoft.Json.Linq.JObject.Parse content
        let sdk = json.Item("sdk") :?> JObject
        let version = sdk.Property("version").Value.ToString()
        version
    with
    | exn -> failwithf "Could not parse global.json: %s" exn.Message

let mutable dotnetExePath = "dotnet"

let deployDir = "./deploy"

let dockerUser = getBuildParam "DockerUser"
// let dockerPassword = getBuildParam "DockerPassword"
// let dockerLoginServer = getBuildParam "DockerLoginServer"
let dockerImageName = getBuildParam "DockerImageName"

// --------------------------------------------------------------------------------------
// END TODO: The rest of the file includes standard build steps
// --------------------------------------------------------------------------------------

let run' timeout cmd args dir =
    if execProcess (fun info ->
        info.FileName <- cmd
        if not (String.IsNullOrWhiteSpace dir) then
            info.WorkingDirectory <- dir
        info.Arguments <- args
    ) timeout |> not then
        failwithf "Error while running '%s' with args: %s" cmd args

let run = run' System.TimeSpan.MaxValue

let runDotnet workingDir args =
    let result =
        ExecProcess (fun info ->
            info.FileName <- dotnetExePath
            info.WorkingDirectory <- workingDir
            info.Arguments <- args) TimeSpan.MaxValue
    if result <> 0 then failwithf "dotnet %s failed" args

let platformTool tool winTool =
    let tool = if isUnix then tool else winTool
    tool
    |> ProcessHelper.tryFindFileOnPath
    |> function Some t -> t | _ -> failwithf "%s not found" tool

let nodeTool = platformTool "node" "node.exe"
let yarnTool = platformTool "yarn" "yarn.cmd"

do if not isWindows then
    // We have to set the FrameworkPathOverride so that dotnet sdk invocations know
    // where to look for full-framework base class libraries
    let mono = platformTool "mono" "mono"
    let frameworkPath = IO.Path.GetDirectoryName(mono) </> ".." </> "lib" </> "mono" </> "4.5"
    setEnvironVar "FrameworkPathOverride" frameworkPath

// ----------------------
// Platform Test Target

Target "ShowPlatform" (fun _ ->
  if isWindows then
    trace "You are on a Windows Box"

  if isUnix then
    trace "You are on a *Nix Box"
  else
    trace "Don't know which box you are on"
)

// --------------------------------------------------------------------------------------
// Clean build results

Target "Clean" (fun _ ->
    !!"src/**/bin"
    ++ "test/**/bin"
    ++ "src/**/obj"
    |> CleanDirs

    CleanDirs ["bin"; "temp"; "docs/output"; deployDir; Path.Combine(clientPath,"public/bundle")]
)

Target "InstallDotNetCore" (fun _ ->
    dotnetExePath <- DotNetCli.InstallDotNetSDK dotnetcliVersion
)

// --------------------------------------------------------------------------------------
// Build library


Target "BuildServer" (fun _ ->
    runDotnet serverPath "build"
)

Target "InstallClient" (fun _ ->
    printfn "Node version:"
    run nodeTool "--version" __SOURCE_DIRECTORY__
    printfn "Yarn version:"
    run yarnTool "--version" __SOURCE_DIRECTORY__
    run yarnTool "install --frozen-lockfile" __SOURCE_DIRECTORY__
)

Target "BuildClient" (fun _ ->
    runDotnet clientPath "restore"
    runDotnet clientPath "fable webpack -- -p"
)


// --------------------------------------------------------------------------------------
// Run the Website

let ipAddress = "localhost"
let port = 8080

FinalTarget "KillProcess" (fun _ ->
    killProcess "dotnet"
    killProcess "dotnet.exe"
)

Target "Run" (fun _ ->
    runDotnet clientPath "restore"
    runDotnet serverPath "restore"

    let unitTestsWatch = async {
        let result =
            ExecProcess (fun info ->
                info.FileName <- dotnetExePath
                info.WorkingDirectory <- serverPath
                info.Arguments <- "watch msbuild /t:RunServer") TimeSpan.MaxValue

        if result <> 0 then failwith "Website shut down." }

    let fablewatch = async { runDotnet clientPath "fable webpack-dev-server" }
    let openBrowser = async {
        System.Threading.Thread.Sleep(5000)
        Diagnostics.Process.Start("http://"+ ipAddress + sprintf ":%d" port) |> ignore }

    Async.Parallel [| unitTestsWatch; fablewatch; openBrowser |]
    |> Async.RunSynchronously
    |> ignore
)


// --------------------------------------------------------------------------------------
// Release Scripts

let publishCmd =
  if isWindows then
    "publish --runtime win-x86 --self-contained"
  else
    "publish"

Target "BundleClient" (fun _ ->
    let result =
        ExecProcess (fun info ->
            info.FileName <- dotnetExePath
            info.WorkingDirectory <- serverPath
            info.Arguments <- publishCmd + " -c Release -o \"" + FullName deployDir + "\"") TimeSpan.MaxValue
    if result <> 0 then failwith "Publish failed"

    !! "src/Server/web.config" |> CopyFiles deployDir

    let clientDir = deployDir </> "client"
    let publicDir = clientDir </> "public"
    let jsDir = clientDir </> "js"
    let imageDir = clientDir </> "images"
    let videoDir = clientDir </> "videos"

    !! "src/Client/public/**/*.*" |> CopyFiles publicDir
    !! "src/Client/js/**/*.*" |> CopyFiles jsDir
    !! "src/Client/images/**/*.*" |> CopyFiles imageDir
    !! "src/Client/videos/**/*.*" |> CopyFiles videoDir

    "src/Client/index.html" |> CopyFile clientDir
)

// this is problematic on *nix systems if you can't run docker as a non-root user
// just let your console fingers swing for this step
Target "CreateDockerImage" (fun _ ->
    if String.IsNullOrEmpty dockerUser then
        failwithf "docker username not given."
    if String.IsNullOrEmpty dockerImageName then
        failwithf "docker image Name not given."
    let result =
        ExecProcess (fun info ->
            info.FileName <- "docker"
            info.UseShellExecute <- false
            info.Arguments <- sprintf "build -t %s/%s ." dockerUser dockerImageName) TimeSpan.MaxValue
    if result <> 0 then failwith "Docker build failed"
)

// -------------------------------------------------------------------------------------
Target "Build" DoNothing
Target "All" DoNothing
Target "OsInfo" DoNothing

"Clean"
  ==> "InstallDotNetCore"
  ==> "InstallClient"
  ==> "BuildServer"
  ==> "BuildClient"
  ==> "BundleClient"
  ==> "CreateDockerImage"
  ==> "All"

"BuildClient"
  ==> "Build"

"InstallClient"
  ==> "Run"

"ShowPlatform"
  ==> "OsInfo"

RunTargetOrDefault "All"
