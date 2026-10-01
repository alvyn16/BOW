param(
    [Parameter(Mandatory = $true)][string]$Directory,
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-fA-F0-9]{40}$')][string]$CertificateThumbprint,
    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'
$Directory = (Resolve-Path -LiteralPath $Directory).Path
$certificate = Get-Item -LiteralPath "Cert:\CurrentUser\My\$CertificateThumbprint"
if (-not $certificate.HasPrivateKey -or $certificate.NotAfter -le (Get-Date) -or $certificate.NotBefore -gt (Get-Date)) {
    throw 'Signing requires a currently valid certificate with an accessible private key.'
}
if (-not ($certificate.EnhancedKeyUsageList.ObjectId -contains '1.3.6.1.5.5.7.3.3')) {
    throw 'This certificate is not authorized for code signing.'
}
$chain = New-Object Security.Cryptography.X509Certificates.X509Chain
try {
    if (-not $chain.Build($certificate)) { throw 'The signing certificate chain is not trusted. Self-signed certificates are not accepted.' }
}
finally { $chain.Dispose() }

$sdkRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
$signTool = Get-ChildItem -LiteralPath $sdkRoot -Directory |
    Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } |
    Sort-Object { [version]$_.Name } -Descending |
    ForEach-Object { Join-Path $_.FullName 'x64\signtool.exe' } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $signTool) { throw 'Windows SDK signtool.exe is required for signing.' }

foreach ($name in @('BOW.exe', 'BOW.dll')) {
    $file = Join-Path $Directory $name
    if (-not (Test-Path -LiteralPath $file)) { throw "Signing input is missing $name." }
    & $signTool sign /sha1 $CertificateThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $file
    if ($LASTEXITCODE -ne 0) { throw "Signing failed for $name." }
    & $signTool verify /pa /all $file
    if ($LASTEXITCODE -ne 0) { throw "Signature verification failed for $name." }
}
Write-Output 'BOW.exe and BOW.dll have verified Authenticode signatures.'
