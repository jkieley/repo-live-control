function Test-FullyQualifiedFileSystemPath {
    param(
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string] $Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $false
    }

    try {
        if (-not [System.IO.Path]::IsPathRooted($Path)) {
            return $false
        }

        $root = [System.IO.Path]::GetPathRoot($Path)
    }
    catch {
        return $false
    }

    if ([string]::IsNullOrWhiteSpace($root)) {
        return $false
    }

    $directorySeparatorRoot = [string] [System.IO.Path]::DirectorySeparatorChar
    $alternateSeparatorRoot = [string] [System.IO.Path]::AltDirectorySeparatorChar
    if ($root -eq $directorySeparatorRoot -or $root -eq $alternateSeparatorRoot) {
        return $false
    }

    if ($root.Length -eq 2 -and $root[1] -eq [char] ':') {
        return $false
    }

    return $true
}

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

    if (-not (Test-FullyQualifiedFileSystemPath -Path $Path)) {
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

function Get-RelativePathWithin {
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
    Assert-PathWithin -Path $fullPath -ParentPath $fullParent -Label $Label

    return $fullPath.Substring($fullParent.Length).TrimStart(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
}
