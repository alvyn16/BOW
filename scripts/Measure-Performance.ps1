param(
    [Parameter(Mandatory = $true)][string]$Executable,
    [ValidateRange(1, 10)][int]$Runs = 3,
    [string]$ReportDirectory = (Join-Path ([IO.Path]::GetTempPath()) ('BOW-perf-' + [guid]::NewGuid()))
)
$ErrorActionPreference = 'Stop'
$Executable = (Resolve-Path -LiteralPath $Executable).Path
New-Item -ItemType Directory -Path $ReportDirectory -Force | Out-Null
$ReportDirectory = (Resolve-Path -LiteralPath $ReportDirectory).Path
$results = @()
for ($run = 1; $run -le $Runs; $run++) {
    $report = Join-Path $ReportDirectory ('run-' + $run + '-' + [guid]::NewGuid() + '.json')
    $process = Start-Process -FilePath $Executable -WorkingDirectory (Split-Path $Executable) `
        -ArgumentList @('--benchmark', ('"' + $report + '"')) -PassThru -WindowStyle Hidden
    try {
        if (-not $process.WaitForExit(240000)) { throw 'Performance benchmark timed out.' }
        if (-not (Test-Path -LiteralPath $report)) { throw 'Benchmark did not produce a report.' }
        $result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
        if ($process.ExitCode -ne 0 -or -not $result.success) { throw "Benchmark failed: $($result.error)" }
        $results += $result
        Write-Output "Run ${run}: startup $([math]::Round($result.startupMs)) ms; switch median $([math]::Round($result.switchMedianMs, 1)) ms"
    }
    finally {
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
        $process.Dispose()
    }
}
function Summary($values) {
    $sorted = @($values | Sort-Object)
    $middle = [int][math]::Floor($sorted.Count / 2)
    $median = $sorted[$middle]
    if ($sorted.Count % 2 -eq 0) { $median = ($sorted[$middle - 1] + $sorted[$middle]) / 2 }
    @{ median = $median; min = $sorted[0]; max = $sorted[-1] }
}
@{ runs = $Runs; startupMs = Summary @($results.startupMs); switchMedianMs = Summary @($results.switchMedianMs) } |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $ReportDirectory 'summary.json') -Encoding utf8
Write-Output "Reports: $ReportDirectory"
