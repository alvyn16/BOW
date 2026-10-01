using BOW.Services;
using System.Security.Cryptography;

namespace BOW.Tests;

public class MicrosoftInstallerTrustTests
{
    [WindowsInstallerFact]
    public void RejectsUnsignedInstaller()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bow-unsigned-{Guid.NewGuid()}.exe");
        try
        {
            File.WriteAllText(path, "This is not a signed installer.");
            Assert.Throws<CryptographicException>(() => MicrosoftInstallerTrust.Verify(path));
        }
        finally { File.Delete(path); }
    }

    [WindowsInstallerFact(true)]
    public void AcceptsMicrosoftInstaller() =>
        MicrosoftInstallerTrust.Verify(Environment.GetEnvironmentVariable("BOW_TEST_INSTALLER")!);

    [WindowsInstallerFact(true)]
    public void RejectsInstallerWithModifiedContent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bow-tampered-{Guid.NewGuid()}.exe");
        try
        {
            var bytes = File.ReadAllBytes(Environment.GetEnvironmentVariable("BOW_TEST_INSTALLER")!);
            bytes[512] ^= 1;
            File.WriteAllBytes(path, bytes);
            Assert.Throws<CryptographicException>(() => MicrosoftInstallerTrust.Verify(path));
        }
        finally { File.Delete(path); }
    }

    private sealed class WindowsInstallerFactAttribute : FactAttribute
    {
        public WindowsInstallerFactAttribute(bool needsInstaller = false)
        {
            if (!OperatingSystem.IsWindows()) Skip = "Authenticode requires Windows.";
            else if (needsInstaller && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BOW_TEST_INSTALLER")))
                Skip = "Set BOW_TEST_INSTALLER to the Microsoft bootstrapper to run this integration test.";
        }
    }
}
