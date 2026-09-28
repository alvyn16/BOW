param(
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [Parameter(Mandatory = $true)][string]$IntermediateDirectory
)

$ErrorActionPreference = 'Stop'
$sdkRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
$makepri = Get-ChildItem -LiteralPath $sdkRoot -Directory |
    Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } |
    Sort-Object { [version]$_.Name } -Descending |
    ForEach-Object { Join-Path $_.FullName 'x64\makepri.exe' } |
    Where-Object { Test-Path -LiteralPath $_ } |
    Select-Object -First 1

if (-not $makepri) { throw 'Windows SDK makepri.exe is required to build BOW.' }

$stage = Join-Path $IntermediateDirectory 'bow-pri'
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $OutputDirectory 'App.xbf') -Destination (Join-Path $stage 'App.xbf') -Force

& $makepri new /pr $stage /cf (Join-Path $PSScriptRoot 'priconfig.xml') /of (Join-Path $OutputDirectory 'resources.pri') /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makepri.exe failed with exit code $LASTEXITCODE" }
