param(
    [Parameter(Mandatory = $true)][string]$Executable,
    [string]$ReportDirectory = (Join-Path $PSScriptRoot '../TestResults/interactions')
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$Executable = (Resolve-Path -LiteralPath $Executable).Path
& dotnet build (Join-Path $repo 'src/BOW.InteractionTests/BOW.InteractionTests.csproj') -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Interaction test build failed.' }
$runner = Join-Path $repo 'src/BOW.InteractionTests/bin/Release/net10.0-windows/BOW.InteractionTests.exe'
# Run the executable so its DPI manifest applies to physical mouse coordinates.
& $runner $Executable ([IO.Path]::GetFullPath($ReportDirectory))
if ($LASTEXITCODE -ne 0) { throw "Browser interactions failed. See $ReportDirectory/interactions.json." }
