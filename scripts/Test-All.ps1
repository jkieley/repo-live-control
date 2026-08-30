[CmdletBinding()]
param(
    [Parameter()]
    [string] $RepoGamePath = 'C:\Program Files (x86)\Steam\steamapps\common\REPO',

    [Parameter()]
    [string] $RepoProfilePath = (Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Thunderstore Mod Manager\DataFolder\REPO\profiles\modded'),

    [Parameter()]
    [switch] $SkipPowerShell51Compatibility
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$commonScriptPath = Join-Path $PSScriptRoot 'PowerShell.Common.ps1'
if (-not (Test-Path -LiteralPath $commonScriptPath -PathType Leaf)) {
    throw "PowerShell compatibility helpers were not found: $commonScriptPath"
}
. $commonScriptPath

function Invoke-DotNet {
    param(
        [Parameter(Mandatory)]
        [string[]] $Arguments
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet exited with code $LASTEXITCODE."
    }
}

Get-Command dotnet -ErrorAction Stop | Out-Null

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).ProviderPath

if (-not $SkipPowerShell51Compatibility) {
    $windowsPowerShell = Get-Command powershell.exe -ErrorAction SilentlyContinue
    if ($null -ne $windowsPowerShell) {
        $compatibilityTestPath = Join-Path $repositoryRoot 'tests\PowerShell51.ScriptCompatibility.Tests.ps1'
        if (-not (Test-Path -LiteralPath $compatibilityTestPath -PathType Leaf)) {
            throw "Windows PowerShell compatibility test was not found: $compatibilityTestPath"
        }

        Write-Host 'Running Windows PowerShell 5.1 script compatibility tests...'
        & $windowsPowerShell.Source `
            -NoProfile `
            -ExecutionPolicy Bypass `
            -File $compatibilityTestPath
        if ($LASTEXITCODE -ne 0) {
            throw "Windows PowerShell compatibility tests exited with code $LASTEXITCODE."
        }
    }
}

$gamePath = Resolve-ExistingAbsoluteDirectory -Path $RepoGamePath -Label 'R.E.P.O. game path'
$profilePath = Resolve-ExistingAbsoluteDirectory -Path $RepoProfilePath -Label 'Thunderstore profile path'
$managedPath = Join-Path $gamePath 'REPO_Data\Managed'
$bepInExPath = Join-Path $profilePath 'BepInEx'

if (-not (Test-Path -LiteralPath $managedPath -PathType Container)) {
    throw "R.E.P.O. managed assembly directory was not found: $managedPath"
}

if (-not (Test-Path -LiteralPath $bepInExPath -PathType Container)) {
    throw "The selected profile does not contain BepInEx: $bepInExPath"
}

$testProjectPath = Join-Path $repositoryRoot 'tests\RepoLiveControl.CommandTests\RepoLiveControl.CommandTests.csproj'
$modProjectPath = Join-Path $repositoryRoot 'src\RepoLiveControl\RepoLiveControl.csproj'
foreach ($projectPath in @($testProjectPath, $modProjectPath)) {
    if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
        throw "Required project was not found: $projectPath"
    }
}

Write-Host 'Running command, fuzzy-completion, network, and session tests...'
Invoke-DotNet -Arguments @(
    'run',
    '--project',
    $testProjectPath,
    '--configuration',
    'Release'
)

Write-Host 'Building RepoCommandConsole in Release configuration...'
Invoke-DotNet -Arguments @(
    'build',
    $modProjectPath,
    '--configuration',
    'Release',
    "-p:RepoGamePath=$gamePath",
    "-p:RepoProfilePath=$profilePath"
)

$releaseDll = Join-Path $repositoryRoot 'src\RepoLiveControl\bin\Release\netstandard2.1\RepoCommandConsole.dll'
if (-not (Test-Path -LiteralPath $releaseDll -PathType Leaf)) {
    throw "Release build did not produce the expected DLL: $releaseDll"
}

Write-Host "PASS: command/network tests and Release build completed: $releaseDll"
