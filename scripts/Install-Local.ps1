[CmdletBinding()]
param(
    [Parameter()]
    [string] $RepoGamePath = 'C:\Program Files (x86)\Steam\steamapps\common\REPO',

    [Parameter()]
    [string] $RepoProfilePath = (Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Thunderstore Mod Manager\DataFolder\REPO\profiles\modded'),

    [Parameter()]
    [switch] $QuarantineLegacyPlugins
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

function Assert-PathWithin {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [string] $ParentPath,

        [Parameter(Mandatory)]
        [string] $Label
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $fullParent = [System.IO.Path]::GetFullPath($ParentPath).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $requiredPrefix = $fullParent + [System.IO.Path]::DirectorySeparatorChar

    if (-not $fullPath.StartsWith($requiredPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label is outside the expected directory '$fullParent': $fullPath"
    }
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

$projectPath = Join-Path $repositoryRoot 'src\RepoLiveControl\RepoLiveControl.csproj'
if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw "Mod project was not found: $projectPath"
}

Invoke-DotNet -Arguments @(
    'build',
    $projectPath,
    '--configuration',
    'Release',
    "-p:RepoGamePath=$gamePath",
    "-p:RepoProfilePath=$profilePath"
)

$sourceDll = Join-Path $repositoryRoot 'src\RepoLiveControl\bin\Release\netstandard2.1\RepoCommandConsole.dll'
if (-not (Test-Path -LiteralPath $sourceDll -PathType Leaf)) {
    throw "Release build did not produce the expected DLL: $sourceDll"
}

$pluginsPath = [System.IO.Path]::GetFullPath((Join-Path $bepInExPath 'plugins'))
Assert-PathWithin -Path $pluginsPath -ParentPath $profilePath -Label 'BepInEx plugins directory'
[System.IO.Directory]::CreateDirectory($pluginsPath) | Out-Null

if ($QuarantineLegacyPlugins) {
    $legacyNamePattern = '^(?:RepoLiveControl|RepoSpawnBridge)(?:V[0-9]+)?\.dll$'
    $legacyFiles = @(
        Get-ChildItem -LiteralPath $pluginsPath -Recurse -File | Where-Object {
            $_.Name -match $legacyNamePattern
        }
    )

    if ($legacyFiles.Count -gt 0) {
        $quarantinePath = Join-Path $bepInExPath ('quarantine\RepoCommandConsole-legacy-' + (Get-Date -Format 'yyyyMMdd-HHmmssfff'))
        $quarantinePath = [System.IO.Path]::GetFullPath($quarantinePath)
        Assert-PathWithin -Path $quarantinePath -ParentPath $profilePath -Label 'Legacy plugin quarantine directory'
        [System.IO.Directory]::CreateDirectory($quarantinePath) | Out-Null

        foreach ($legacyFile in $legacyFiles) {
            $sourcePath = [System.IO.Path]::GetFullPath($legacyFile.FullName)
            Assert-PathWithin -Path $sourcePath -ParentPath $pluginsPath -Label 'Legacy plugin DLL'
            if ([System.IO.Path]::GetFileName($sourcePath) -notmatch $legacyNamePattern) {
                throw "Refusing to quarantine an unexpected file: $sourcePath"
            }

            $relativePath = [System.IO.Path]::GetRelativePath($profilePath, $sourcePath)
            $destinationPath = [System.IO.Path]::GetFullPath((Join-Path $quarantinePath $relativePath))
            Assert-PathWithin -Path $destinationPath -ParentPath $quarantinePath -Label 'Legacy plugin quarantine destination'
            [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($destinationPath)) | Out-Null
            Move-Item -LiteralPath $sourcePath -Destination $destinationPath
            Write-Host "Quarantined legacy plugin: $sourcePath -> $destinationPath"
        }

        Write-Host "Legacy DLLs can be recovered from: $quarantinePath"
    }
    else {
        Write-Host 'No legacy RepoLiveControl[Version].dll or RepoSpawnBridge[Version].dll files were found.'
    }
}

$installPath = [System.IO.Path]::GetFullPath((Join-Path $pluginsPath 'JamesKieley-RepoCommandConsole'))
Assert-PathWithin -Path $installPath -ParentPath $pluginsPath -Label 'Mod install directory'
[System.IO.Directory]::CreateDirectory($installPath) | Out-Null

$destinationDll = [System.IO.Path]::GetFullPath((Join-Path $installPath 'RepoCommandConsole.dll'))
Assert-PathWithin -Path $destinationDll -ParentPath $installPath -Label 'Installed mod DLL'
Copy-Item -LiteralPath $sourceDll -Destination $destinationDll -Force

$sourceHash = (Get-FileHash -LiteralPath $sourceDll -Algorithm SHA256).Hash
$destinationHash = (Get-FileHash -LiteralPath $destinationDll -Algorithm SHA256).Hash
if ($sourceHash -ne $destinationHash) {
    throw "Installed DLL hash did not match the build output: $destinationDll"
}

Write-Host "Installed RepoCommandConsole.dll: $destinationDll"
Write-Host 'Restart R.E.P.O. and launch it with Start Modded to load this build.'
Get-Item -LiteralPath $destinationDll
