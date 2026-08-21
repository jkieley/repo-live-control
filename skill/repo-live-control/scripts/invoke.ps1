param(
    [Parameter(Position = 0, Mandatory = $true)]
    [ValidateSet("enemy", "loot", "item", "despawn", "auto", "unstick", "status")]
    [string]$Action,

    [Parameter(Position = 1)]
    [string]$Target = "random",

    [Parameter(Position = 2)]
    [int]$Count = 1,

    [Parameter(Position = 3)]
    [ValidateSet("safe", "near-player", "at-player")]
    [string]$Placement = "safe"
)

$controller = "C:\Users\James Kieley\Documents\Codex\2026-08-18\search-the-web-are-there-any\repo-live-control\scripts\RepoControl.ps1"
& $controller $Action $Target $Count $Placement
exit $LASTEXITCODE
