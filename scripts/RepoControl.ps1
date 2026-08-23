param(
    [Parameter(Position = 0, Mandatory = $true)]
    [ValidateSet("enemy", "loot", "item", "despawn", "despawnitem", "auto", "unstick", "status")]
    [string]$Action,

    [Parameter(Position = 1)]
    [string]$Target = "random",

    [Parameter(Position = 2)]
    [int]$Count = 1,

    [Parameter(Position = 3)]
    [ValidateSet("safe", "near-player", "at-player")]
    [string]$Placement = "safe",

    [int]$TimeoutMilliseconds = 30000
)

$pipe = [System.IO.Pipes.NamedPipeClientStream]::new(
    ".",
    "CodexRepoLiveControlV5",
    [System.IO.Pipes.PipeDirection]::InOut)

try {
    $pipe.Connect(2000)
    $encoding = [System.Text.UTF8Encoding]::new($false)
    $writer = [System.IO.StreamWriter]::new($pipe, $encoding, 1024, $true)
    $reader = [System.IO.StreamReader]::new($pipe, $encoding, $false, 1024, $true)
    $writer.AutoFlush = $true

    switch ($Action) {
        "despawn" { $command = "despawn|$Target|$Count" }
        "despawnitem" { $command = "despawnitem|$Target" }
        "auto" { $command = "auto|$Target" }
        "unstick" { $command = "unstick|loot" }
        "status" { $command = "status" }
        default { $command = "$Action|$Target|$Count|$Placement" }
    }

    $writer.WriteLine($command)
    $readTask = $reader.ReadLineAsync()
    if (-not $readTask.Wait($TimeoutMilliseconds)) {
        throw "Timed out waiting for the REPO game thread."
    }

    $response = $readTask.Result
    if ([string]::IsNullOrWhiteSpace($response)) {
        throw "The bridge closed without returning a response."
    }

    Write-Output $response
    if ($response.StartsWith("ERROR")) {
        exit 1
    }
}
finally {
    $pipe.Dispose()
}
