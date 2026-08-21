param(
    [Parameter(Position = 0)]
    [string]$Enemy = "high",

    [Parameter(Position = 1)]
    [ValidateRange(1, 10)]
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
    $writer.WriteLine("enemy|$Enemy|$Count")
    $writer.Dispose()
    Write-Output "Queued $Count x '$Enemy' enemy spawn."
}
finally {
    $pipe.Dispose()
}
