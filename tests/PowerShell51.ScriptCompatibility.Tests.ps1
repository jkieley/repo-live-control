[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-True {
    param(
        [Parameter(Mandatory)]
        [bool] $Condition,

        [Parameter(Mandatory)]
        [string] $Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-ExpectedFailure {
    param(
        [Parameter(Mandatory)]
        [scriptblock] $Action,

        [Parameter(Mandatory)]
        [string] $MessagePattern,

        [Parameter(Mandatory)]
        [string] $Label
    )

    try {
        & $Action | Out-Null
    }
    catch {
        if ($_.Exception.Message -notmatch $MessagePattern) {
            throw "$Label failed with an unexpected error: $($_.Exception.Message)"
        }

        return
    }

    throw "$Label unexpectedly succeeded."
}

if ($PSVersionTable.PSEdition -ne 'Desktop' -or $PSVersionTable.PSVersion.Major -ne 5) {
    throw "This regression test must run under Windows PowerShell 5.1; found $($PSVersionTable.PSEdition) $($PSVersionTable.PSVersion)."
}

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).ProviderPath
$temporaryParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar)
$temporaryName = 'RepoCommandConsole-ps51-' + [System.Guid]::NewGuid().ToString('N')
$temporaryRoot = [System.IO.Path]::GetFullPath((Join-Path $temporaryParent $temporaryName))
$requiredTemporaryPrefix = $temporaryParent + [System.IO.Path]::DirectorySeparatorChar
if (-not $temporaryRoot.StartsWith($requiredTemporaryPrefix, [System.StringComparison]::OrdinalIgnoreCase) -or
    [System.IO.Path]::GetFileName($temporaryRoot) -notmatch '^RepoCommandConsole-ps51-[a-f0-9]{32}$') {
    throw "Refusing to use an unexpected temporary test path: $temporaryRoot"
}

$originalPath = $env:PATH
$originalLogPath = $env:DOTNET_STUB_LOG
$exitCode = 0

try {
    $fixtureScripts = Join-Path $temporaryRoot 'scripts'
    $fixtureTests = Join-Path $temporaryRoot 'tests\RepoLiveControl.CommandTests'
    $fixtureOutput = Join-Path $temporaryRoot 'src\RepoLiveControl\bin\Release\netstandard2.1'
    $fixtureGame = Join-Path $temporaryRoot 'game'
    $fixtureManaged = Join-Path $fixtureGame 'REPO_Data\Managed'
    $fixtureProfile = Join-Path $temporaryRoot 'profile'
    $fixtureBepInEx = Join-Path $fixtureProfile 'BepInEx'
    $fixtureLegacyDirectory = Join-Path $fixtureBepInEx 'plugins\nested'
    $fixtureBin = Join-Path $temporaryRoot 'bin'

    foreach ($directory in @(
        $fixtureScripts,
        $fixtureTests,
        $fixtureOutput,
        $fixtureManaged,
        $fixtureLegacyDirectory,
        $fixtureBin
    )) {
        [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    }

    foreach ($scriptName in @('Test-All.ps1', 'Install-Local.ps1', 'PowerShell.Common.ps1')) {
        Copy-Item `
            -LiteralPath (Join-Path $repositoryRoot "scripts\$scriptName") `
            -Destination (Join-Path $fixtureScripts $scriptName)
    }

    [System.IO.File]::WriteAllText(
        (Join-Path $fixtureTests 'RepoLiveControl.CommandTests.csproj'),
        '<Project />')
    [System.IO.Directory]::CreateDirectory((Join-Path $temporaryRoot 'src\RepoLiveControl')) | Out-Null
    [System.IO.File]::WriteAllText(
        (Join-Path $temporaryRoot 'src\RepoLiveControl\RepoLiveControl.csproj'),
        '<Project />')

    $sentinelBytes = [byte[]] @(82, 69, 80, 79, 45, 80, 83, 53, 49)
    $fixtureDll = Join-Path $fixtureOutput 'RepoCommandConsole.dll'
    [System.IO.File]::WriteAllBytes($fixtureDll, $sentinelBytes)

    $legacyDll = Join-Path $fixtureLegacyDirectory 'RepoLiveControlV9.dll'
    [System.IO.File]::WriteAllBytes($legacyDll, [byte[]] @(76, 69, 71, 65, 67, 89))

    $stubLog = Join-Path $temporaryRoot 'dotnet-calls.log'
    $stubPath = Join-Path $fixtureBin 'dotnet.cmd'
    [System.IO.File]::WriteAllText(
        $stubPath,
        "@echo off`r`necho %*>> `"%DOTNET_STUB_LOG%`"`r`nexit /b 0`r`n")
    $env:DOTNET_STUB_LOG = $stubLog
    $env:PATH = $fixtureBin + [System.IO.Path]::PathSeparator + $originalPath

    . (Join-Path $fixtureScripts 'PowerShell.Common.ps1')
    Assert-True (Test-FullyQualifiedFileSystemPath -Path 'C:\fixture') 'Drive-rooted path was rejected.'
    Assert-True (-not (Test-FullyQualifiedFileSystemPath -Path 'C:fixture')) 'Drive-relative path was accepted.'
    Assert-True (-not (Test-FullyQualifiedFileSystemPath -Path '\fixture')) 'Root-relative path was accepted.'
    Assert-True (-not (Test-FullyQualifiedFileSystemPath -Path 'fixture')) 'Relative path was accepted.'

    $testAllPath = Join-Path $fixtureScripts 'Test-All.ps1'
    & $testAllPath `
        -RepoGamePath $fixtureGame `
        -RepoProfilePath $fixtureProfile `
        -SkipPowerShell51Compatibility | Out-Null

    $testCalls = @(Get-Content -LiteralPath $stubLog)
    Assert-True ($testCalls.Count -eq 2) "Expected two dotnet calls from Test-All.ps1; found $($testCalls.Count)."

    [System.IO.File]::WriteAllText($stubLog, '')
    $installPath = Join-Path $fixtureScripts 'Install-Local.ps1'
    & $installPath `
        -RepoGamePath $fixtureGame `
        -RepoProfilePath $fixtureProfile `
        -QuarantineLegacyPlugins `
        -SkipRunningGameCheck | Out-Null

    $installCalls = @(Get-Content -LiteralPath $stubLog | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    Assert-True ($installCalls.Count -eq 1) "Expected one dotnet call from Install-Local.ps1; found $($installCalls.Count)."

    $installedDll = Join-Path $fixtureBepInEx 'plugins\Coollectors-RepoCommandConsole\RepoCommandConsole.dll'
    Assert-True (Test-Path -LiteralPath $installedDll -PathType Leaf) 'Install-Local.ps1 did not install the DLL in the fixture profile.'
    Assert-True (
        ((Get-FileHash -LiteralPath $fixtureDll -Algorithm SHA256).Hash -eq
            (Get-FileHash -LiteralPath $installedDll -Algorithm SHA256).Hash)
    ) 'Fixture install DLL hash did not match the source sentinel.'
    Assert-True (-not (Test-Path -LiteralPath $legacyDll)) 'Legacy DLL remained in the fixture plugins directory.'

    $quarantinedLegacyFiles = @(
        Get-ChildItem `
            -LiteralPath (Join-Path $fixtureBepInEx 'quarantine') `
            -Filter 'RepoLiveControlV9.dll' `
            -File `
            -Recurse
    )
    Assert-True ($quarantinedLegacyFiles.Count -eq 1) "Expected one quarantined legacy DLL; found $($quarantinedLegacyFiles.Count)."

    Assert-ExpectedFailure `
        -Action {
            & $testAllPath `
                -RepoGamePath '.' `
                -RepoProfilePath $fixtureProfile `
                -SkipPowerShell51Compatibility
        } `
        -MessagePattern 'must be an absolute path' `
        -Label 'Test-All.ps1 relative-path validation'

    Assert-ExpectedFailure `
        -Action {
            & $installPath `
                -RepoGamePath '.' `
                -RepoProfilePath $fixtureProfile `
                -SkipRunningGameCheck
        } `
        -MessagePattern 'must be an absolute path' `
        -Label 'Install-Local.ps1 relative-path validation'

    Write-Host 'PASS: Windows PowerShell 5.1 path validation, build invocation, install, and legacy quarantine compatibility.'
}
catch {
    Write-Error -Message $_.Exception.Message -ErrorAction Continue
    $exitCode = 1
}
finally {
    $env:PATH = $originalPath
    $env:DOTNET_STUB_LOG = $originalLogPath

    if (Test-Path -LiteralPath $temporaryRoot) {
        $resolvedTemporaryRoot = [System.IO.Path]::GetFullPath($temporaryRoot)
        if ($resolvedTemporaryRoot.StartsWith($requiredTemporaryPrefix, [System.StringComparison]::OrdinalIgnoreCase) -and
            [System.IO.Path]::GetFileName($resolvedTemporaryRoot) -match '^RepoCommandConsole-ps51-[a-f0-9]{32}$') {
            Remove-Item -LiteralPath $resolvedTemporaryRoot -Recurse -Force
        }
        else {
            Write-Error `
                -Message "Refusing to remove an unexpected temporary test path: $resolvedTemporaryRoot" `
                -ErrorAction Continue
            $exitCode = 1
        }
    }
}

exit $exitCode
