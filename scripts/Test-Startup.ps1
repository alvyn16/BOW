param(
    [Parameter(Mandatory = $true)][string]$Executable,
    [string]$ReportDirectory = (Join-Path ([IO.Path]::GetTempPath()) ('BOW-smoke-' + [guid]::NewGuid())),
    [ValidateRange(5, 60)][int]$TimeoutSeconds = 45
)

$ErrorActionPreference = 'Stop'
$Executable = (Resolve-Path -LiteralPath $Executable).Path
New-Item -ItemType Directory -Path $ReportDirectory -Force | Out-Null
$report = Join-Path (Resolve-Path -LiteralPath $ReportDirectory).Path ('startup-' + [guid]::NewGuid() + '.json')
$process = Start-Process -FilePath $Executable -WorkingDirectory (Split-Path $Executable) `
    -ArgumentList @('--smoke-test', ('"' + $report + '"')) -PassThru -WindowStyle Hidden
try {
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        throw "BOW startup check timed out after $TimeoutSeconds seconds."
    }
    if (-not (Test-Path -LiteralPath $report)) {
        throw "BOW exited with code $($process.ExitCode) without a startup report."
    }
    $result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    if ($process.ExitCode -ne 0 -or -not $result.success) {
        throw "BOW startup check failed: $($result.error) (exit $($process.ExitCode))."
    }
    Write-Output "BOW startup passed; WebView2 $($result.runtimeVersion). Report: $report"
}
finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    $process.Dispose()
}
