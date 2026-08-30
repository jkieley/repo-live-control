[CmdletBinding()]
param(
    [Parameter()]
    [string] $RepoGamePath = 'C:\Program Files (x86)\Steam\steamapps\common\REPO',

    [Parameter()]
    [string] $RepoProfilePath = (Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Thunderstore Mod Manager\DataFolder\REPO\profiles\modded')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$commonScriptPath = Join-Path $PSScriptRoot 'PowerShell.Common.ps1'
if (-not (Test-Path -LiteralPath $commonScriptPath -PathType Leaf)) {
    throw "PowerShell compatibility helpers were not found: $commonScriptPath"
}
. $commonScriptPath

$PackageName = 'RepoCommandConsole'
$PackageVersion = '2.0.0'
$PackageFileName = "JamesKieley-$PackageName-$PackageVersion.zip"
$ExpectedWebsite = 'https://github.com/jkieley/repo-live-control'
$ExpectedDependencies = @(
    'BepInEx-BepInExPack-5.4.2305',
    'Zehs-REPOLib-4.2.0'
)

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

function Assert-Manifest {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    try {
        $manifest = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    }
    catch {
        throw "Thunderstore manifest is not valid JSON: $($_.Exception.Message)"
    }

    if ($manifest.name -ne $PackageName -or $manifest.name -notmatch '^[A-Za-z0-9_]+$') {
        throw "Manifest name must be exactly '$PackageName' and contain only letters, digits, or underscores."
    }

    if ($manifest.version_number -ne $PackageVersion -or $manifest.version_number -notmatch '^\d+\.\d+\.\d+$') {
        throw "Manifest version_number must be exactly '$PackageVersion'."
    }

    if ($manifest.website_url -ne $ExpectedWebsite) {
        throw "Manifest website_url must be exactly '$ExpectedWebsite'."
    }

    if ([string]::IsNullOrWhiteSpace([string] $manifest.description) -or ([string] $manifest.description).Length -gt 250) {
        throw 'Manifest description must contain between 1 and 250 characters.'
    }

    $dependencies = @($manifest.dependencies)
    if ($dependencies.Count -ne $ExpectedDependencies.Count) {
        throw "Manifest must contain exactly $($ExpectedDependencies.Count) dependencies."
    }

    for ($index = 0; $index -lt $ExpectedDependencies.Count; $index++) {
        if ($dependencies[$index] -ne $ExpectedDependencies[$index]) {
            throw "Manifest dependency $index must be '$($ExpectedDependencies[$index])'."
        }
    }
}

function Assert-Png256 {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $signature = [byte[]] @(137, 80, 78, 71, 13, 10, 26, 10)
    if ($bytes.Length -lt 24) {
        throw "Icon is too short to be a PNG: $Path"
    }

    for ($index = 0; $index -lt $signature.Length; $index++) {
        if ($bytes[$index] -ne $signature[$index]) {
            throw "Icon does not have a valid PNG signature: $Path"
        }
    }

    $chunkName = [System.Text.Encoding]::ASCII.GetString($bytes, 12, 4)
    if ($chunkName -ne 'IHDR') {
        throw "Icon PNG does not begin with an IHDR chunk: $Path"
    }

    [int64] $width = ([int64] $bytes[16] * 16777216) +
        ([int64] $bytes[17] * 65536) +
        ([int64] $bytes[18] * 256) +
        [int64] $bytes[19]
    [int64] $height = ([int64] $bytes[20] * 16777216) +
        ([int64] $bytes[21] * 65536) +
        ([int64] $bytes[22] * 256) +
        [int64] $bytes[23]

    if ($width -ne 256 -or $height -ne 256) {
        throw "Thunderstore icon must be exactly 256x256 pixels; found ${width}x${height}."
    }
}

Get-Command dotnet -ErrorAction Stop | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem

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
$manifestSource = Join-Path $repositoryRoot 'thunderstore\manifest.json'
$readmeSource = Join-Path $repositoryRoot 'README.md'
$iconSource = Join-Path $repositoryRoot 'thunderstore\icon.png'
$changelogSource = Join-Path $repositoryRoot 'thunderstore\CHANGELOG.md'
$requiredSources = @($projectPath, $manifestSource, $readmeSource, $iconSource, $changelogSource)
foreach ($requiredSource in $requiredSources) {
    if (-not (Test-Path -LiteralPath $requiredSource -PathType Leaf)) {
        throw "Required package source file was not found: $requiredSource"
    }
}

Assert-Manifest -Path $manifestSource
Assert-Png256 -Path $iconSource

Invoke-DotNet -Arguments @(
    'build',
    $projectPath,
    '--configuration',
    'Release',
    "-p:RepoGamePath=$gamePath",
    "-p:RepoProfilePath=$profilePath"
)

$dllSource = Join-Path $repositoryRoot 'src\RepoLiveControl\bin\Release\netstandard2.1\RepoCommandConsole.dll'
if (-not (Test-Path -LiteralPath $dllSource -PathType Leaf)) {
    throw "Release build did not produce the expected DLL: $dllSource"
}

$distPath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'dist'))
Assert-PathWithin -Path $distPath -ParentPath $repositoryRoot -Label 'Distribution directory'
[System.IO.Directory]::CreateDirectory($distPath) | Out-Null

$stagePath = Join-Path $distPath ('.package-stage-' + [System.Guid]::NewGuid().ToString('N'))
$temporaryZipPath = Join-Path $distPath ('.package-' + [System.Guid]::NewGuid().ToString('N') + '.zip')
$packagePath = Join-Path $distPath $PackageFileName
Assert-PathWithin -Path $stagePath -ParentPath $distPath -Label 'Package staging directory'
Assert-PathWithin -Path $temporaryZipPath -ParentPath $distPath -Label 'Temporary package path'
Assert-PathWithin -Path $packagePath -ParentPath $distPath -Label 'Final package path'

try {
    [System.IO.Directory]::CreateDirectory($stagePath) | Out-Null

    Copy-Item -LiteralPath $manifestSource -Destination (Join-Path $stagePath 'manifest.json')
    Copy-Item -LiteralPath $readmeSource -Destination (Join-Path $stagePath 'README.md')
    Copy-Item -LiteralPath $iconSource -Destination (Join-Path $stagePath 'icon.png')
    Copy-Item -LiteralPath $changelogSource -Destination (Join-Path $stagePath 'CHANGELOG.md')
    Copy-Item -LiteralPath $dllSource -Destination (Join-Path $stagePath 'RepoCommandConsole.dll')

    $expectedEntries = @(
        'CHANGELOG.md',
        'icon.png',
        'manifest.json',
        'README.md',
        'RepoCommandConsole.dll'
    ) | Sort-Object
    $stagedEntries = @(Get-ChildItem -LiteralPath $stagePath -File | ForEach-Object Name | Sort-Object)
    if (Compare-Object -ReferenceObject $expectedEntries -DifferenceObject $stagedEntries) {
        throw 'Package staging directory does not contain exactly the required five files.'
    }

    Assert-Manifest -Path (Join-Path $stagePath 'manifest.json')
    Assert-Png256 -Path (Join-Path $stagePath 'icon.png')

    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $stagePath,
        $temporaryZipPath,
        [System.IO.Compression.CompressionLevel]::Optimal,
        $false)

    $archive = [System.IO.Compression.ZipFile]::OpenRead($temporaryZipPath)
    try {
        $archiveEntries = @($archive.Entries | ForEach-Object FullName | Sort-Object)
        if (Compare-Object -ReferenceObject $expectedEntries -DifferenceObject $archiveEntries) {
            throw 'Generated archive does not contain exactly the required root files.'
        }
    }
    finally {
        $archive.Dispose()
    }

    if (Test-Path -LiteralPath $packagePath -PathType Leaf) {
        $backupPath = $packagePath + '.previous-' + (Get-Date -Format 'yyyyMMdd-HHmmssfff')
        Assert-PathWithin -Path $backupPath -ParentPath $distPath -Label 'Previous package backup path'
        [System.IO.File]::Replace($temporaryZipPath, $packagePath, $backupPath, $true)
        Write-Host "Previous package preserved at: $backupPath"
    }
    else {
        [System.IO.File]::Move($temporaryZipPath, $packagePath)
    }

    Write-Host "Thunderstore package created: $packagePath"
    Get-Item -LiteralPath $packagePath
}
finally {
    if (Test-Path -LiteralPath $stagePath) {
        Assert-PathWithin -Path $stagePath -ParentPath $distPath -Label 'Package staging cleanup target'
        Remove-Item -LiteralPath $stagePath -Recurse -Force
    }

    if (Test-Path -LiteralPath $temporaryZipPath -PathType Leaf) {
        Assert-PathWithin -Path $temporaryZipPath -ParentPath $distPath -Label 'Temporary package cleanup target'
        Remove-Item -LiteralPath $temporaryZipPath -Force
    }
}
