param(
    [string]$ProfilePath,
    [string]$GamePath,
    [switch]$InstallOnly
)

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\RepoLiveControl\RepoLiveControl.csproj"
$process = Get-Process -Name REPO -ErrorAction SilentlyContinue | Select-Object -First 1

if ($process) {
    if (-not $GamePath) {
        $GamePath = Split-Path -Parent $process.Path
    }
    if (-not $ProfilePath) {
        $commandLine = (Get-CimInstance Win32_Process -Filter "ProcessId=$($process.Id)").CommandLine
        if ($commandLine -match '--doorstop-target-assembly\s+"([^"]+)"') {
            $preloader = $matches[1].Replace('/', '\')
            $core = Split-Path -Parent $preloader
            $bepInEx = Split-Path -Parent $core
            $ProfilePath = Split-Path -Parent $bepInEx
        }
    }
}

if (-not $GamePath) {
    $GamePath = "C:\Program Files (x86)\Steam\steamapps\common\REPO"
}
if (-not $ProfilePath) {
    throw "ProfilePath is required when it cannot be inferred from a running modded REPO process."
}

dotnet build $project -c Release --nologo --verbosity minimal "/p:RepoGamePath=$GamePath" "/p:RepoProfilePath=$ProfilePath"
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$built = Join-Path $repoRoot "src\RepoLiveControl\bin\Release\netstandard2.1\RepoCommandConsole.dll"
$pluginDir = Join-Path $ProfilePath "BepInEx\plugins\Coollectors-RepoCommandConsole"
New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
$installed = Join-Path $pluginDir "RepoCommandConsole.dll"
Copy-Item -LiteralPath $built -Destination $installed -Force

if ($InstallOnly) {
    Write-Output "Installed RepoCommandConsole.dll for the next game launch. Restart R.E.P.O. to load the UI."
    exit 0
}
if (-not $process) {
    throw "REPO is not running. The plugin was installed but could not be attached live."
}

$toolRoot = Join-Path $env:TEMP "codex-repo-live-fix"
$injector = Join-Path $toolRoot "SharpMonoInjector.Console\smi.exe"
if (-not (Test-Path -LiteralPath $injector)) {
    New-Item -ItemType Directory -Force -Path $toolRoot | Out-Null
    $zip = Join-Path $toolRoot "SharpMonoInjector.Console.zip"
    Invoke-WebRequest -Uri "https://github.com/warbler/SharpMonoInjector/releases/download/v2.2/SharpMonoInjector.Console.zip" -OutFile $zip
    Expand-Archive -LiteralPath $zip -DestinationPath $toolRoot -Force
}

& $injector inject -p REPO -a $built -n RepoLiveControl -c Loader -m Load
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Output "The RepoCommandConsole bridge is installed and attached. Restart R.E.P.O. to load the in-game UI."
