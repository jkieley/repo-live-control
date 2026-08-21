param(
    [Parameter(Position = 0, Mandatory = $true)]
    [ValidateSet("enemy", "loot", "despawn", "status")]
    [string]$Action,

    [Parameter(Position = 1)]
    [string]$Target = "random",

    [Parameter(Position = 2)]
    [ValidateRange(1, 50)]
    [int]$Count = 1
)

$pipe = [System.IO.Pipes.NamedPipeClientStream]::new(
    ".",
    "CodexRepoControlV1",
    [System.IO.Pipes.PipeDirection]::Out)

try {
    $pipe.Connect(1500)
    $writer = [System.IO.StreamWriter]::new($pipe)
    $writer.AutoFlush = $true
    $writer.WriteLine("$Action|$Target|$Count")
    $writer.Dispose()
    Write-Output "Queued REPO control request: $Action $Target $Count"
}
finally {
    $pipe.Dispose()
}
