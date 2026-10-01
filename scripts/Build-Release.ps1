param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\dist'),
    [string]$RuntimeInstallerPath,
    [string]$CertificateThumbprint,
    [switch]$RequireSigning
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repo 'src\BOW\BOW.csproj'
[xml]$projectXml = Get-Content -LiteralPath $project -Raw
$version = [string]$projectXml.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+(?:-[a-zA-Z0-9.-]+)?$') { throw 'BOW.csproj must declare a valid release Version.' }

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
$work = Join-Path ([IO.Path]::GetTempPath()) ('BOW-release-' + [guid]::NewGuid())
$publish = Join-Path $work "BOW-$version-win-x64"
$build = Join-Path $work 'build'
$archiveName = "BOW-$version-win-x64.zip"
$archive = Join-Path $OutputDirectory $archiveName
if (Test-Path -LiteralPath $archive) { throw "Release archive already exists: $archive. Use a new output directory." }
if ($RequireSigning -and -not $CertificateThumbprint) { throw 'A trusted signing certificate is required for this build.' }

& dotnet publish $project -c Release -r win-x64 --self-contained true -p:Platform=x64 `
    "-p:OutputPath=$build\" -o $publish --nologo
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

foreach ($required in @('BOW.exe', 'BOW.dll', 'resources.pri', 'App.xbf', 'Microsoft.UI.Xaml.dll', 'coreclr.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $publish $required))) { throw "Publish output is missing $required." }
}
foreach ($doc in @('LICENSE', 'README.md', 'THIRD-PARTY-NOTICES.md')) {
    Copy-Item -LiteralPath (Join-Path $repo $doc) -Destination $publish
}
Copy-Item -LiteralPath (Join-Path $repo 'docs\INSTALL.md') -Destination $publish
$runtimeFolder = Join-Path $publish 'runtime'
New-Item -ItemType Directory -Path $runtimeFolder -Force | Out-Null
$installer = Join-Path $runtimeFolder 'MicrosoftEdgeWebview2Setup.exe'
if ($RuntimeInstallerPath) {
    Copy-Item -LiteralPath $RuntimeInstallerPath -Destination $installer
}
else { & (Join-Path $PSScriptRoot 'Get-WebView2Installer.ps1') -Destination $installer }
$signature = Get-AuthenticodeSignature -LiteralPath $installer
if ($signature.Status -ne 'Valid' -or
    $signature.SignerCertificate.GetNameInfo([Security.Cryptography.X509Certificates.X509NameType]::SimpleName, $false) -ne 'Microsoft Corporation') {
    throw 'The bundled WebView2 installer must have a valid Microsoft signature.'
}

# Preserve the notices supplied by every resolved package, including runtime packs.
$assets = Get-Content -LiteralPath (Join-Path $repo 'src\BOW\obj\project.assets.json') -Raw | ConvertFrom-Json
$packages = @($assets.libraries.PSObject.Properties | ForEach-Object {
    if ($_.Value.type -eq 'package') { $_.Value.path }
})
$packages += @($assets.project.frameworks.PSObject.Properties | ForEach-Object {
    $_.Value.downloadDependencies | ForEach-Object {
        $resolvedVersion = $_.version.Trim('[', ']').Split(',')[0].Trim()
        ($_.name + '/' + $resolvedVersion).ToLowerInvariant()
    }
})
$noticeRoot = Join-Path $publish 'licenses'
foreach ($package in ($packages | Sort-Object -Unique)) {
    foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
        $packagePath = Join-Path $folder $package
        if (-not (Test-Path -LiteralPath $packagePath)) { continue }
        $notices = @(Get-ChildItem -LiteralPath $packagePath -File | Where-Object {
            $_.Name -match '(?i)license|notice|copying'
        })
        foreach ($notice in $notices) {
            $destination = Join-Path $noticeRoot ($package.Replace('/', '-'))
            New-Item -ItemType Directory -Path $destination -Force | Out-Null
            Copy-Item -LiteralPath $notice.FullName -Destination $destination
        }
        break
    }
}

$versioningNotices = Join-Path $noticeRoot 'nuget.versioning'
New-Item -ItemType Directory -Path $versioningNotices -Force | Out-Null
foreach ($notice in @('Apache-2.0.txt', 'NuGet.Versioning-NOTICE.txt')) {
    Copy-Item -LiteralPath (Join-Path $repo "docs/licenses/$notice") -Destination $versioningNotices
}

if ($CertificateThumbprint) {
    & (Join-Path $PSScriptRoot 'Sign-Release.ps1') -Directory $publish -CertificateThumbprint $CertificateThumbprint
}
@{ version = $version; signed = [bool]$CertificateThumbprint; webView2Setup = 'Microsoft signed Evergreen bootstrapper' } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $publish 'release.json') -Encoding utf8
Compress-Archive -LiteralPath $publish -DestinationPath $archive -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath ($archive + '.sha256') -Value "$hash  $archiveName" -Encoding ascii
Write-Output "Release package: $archive"
Write-Output "SHA-256: $hash"
