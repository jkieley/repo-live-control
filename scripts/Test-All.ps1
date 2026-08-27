[CmdletBinding()]
param(
    [Parameter()]
    [string] $RepoGamePath = 'C:\Program Files (x86)\Steam\steamapps\common\REPO',

    [Parameter()]
    [string] $RepoProfilePath = (Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Thunderstore Mod Manager\DataFolder\REPO\profiles\modded')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-ExistingAbsoluteDirectory {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [string] $Label
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "$Label cannot be empty."
    }

    if (-not [System.IO.Path]::IsPathFullyQualified($Path)) {
        throw "$Label must be an absolute path: $Path"
    }

    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        throw "$Label does not exist or is not a directory: $Path"
    }

    return (Resolve-Path -LiteralPath $Path).ProviderPath
}

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

Write-Host 'Running command parser and fuzzy-completion tests...'
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

Write-Host "PASS: command tests and Release build completed: $releaseDll"
