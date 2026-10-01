# Code signing BOW

The first previews are unsigned. Microsoft's bundled WebView2 installer is
signed by Microsoft; that signature does not sign or endorse BOW itself.

Trusted signing requires a currently valid code-signing certificate issued to
the BOW publisher, with its private key accessible through the Windows certificate
store. The project does not have such an identity configured yet. A self-signed
certificate will not provide public publisher trust and is not accepted by the
release signing script.

## Sign on a configured Windows machine

Install the Windows SDK including SignTool. Provision your certificate/key using
your certificate provider's supported process, then use the certificate thumbprint:

```powershell
./scripts/Build-Release.ps1 -CertificateThumbprint YOUR_40_CHARACTER_THUMBPRINT -RequireSigning
```

The signing script checks the certificate's validity, private-key availability,
code-signing usage, and trusted chain. It signs only `BOW.exe` and `BOW.dll` with
SHA-256, requests an RFC 3161 timestamp, and verifies both signatures before the
ZIP and checksum are produced. Bundled Microsoft and other dependency files keep
their existing signatures. `release.json` records whether BOW was signed.

The default timestamp service is `http://timestamp.digicert.com`; use a provider's
timestamp service with `scripts/Sign-Release.ps1 -TimestampUrl` when needed.
The thumbprint identifies a certificate and is not a private key or password.
Never commit private keys, PFX files, or signing passwords to the repository.

## GitHub releases

GitHub-hosted runners currently have no BOW signing key, so the workflow produces
unsigned preview archives. After selecting a certificate/signing provider,
configure its supported hardware-backed or cloud signing integration, pass the
result through signature verification, then require signing in release builds.
Buying/provisioning an identity and configuring the provider are separate from
adding this build script. No private key is generated or imported by BOW.

A valid Authenticode signature identifies the publisher and protects file
integrity; it does not guarantee that Windows SmartScreen will show no warning.
