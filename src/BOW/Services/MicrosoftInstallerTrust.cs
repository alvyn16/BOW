using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace BOW.Services;

public static class MicrosoftInstallerTrust
{
    public static void Verify(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("The WebView2 installer is missing.", path);
        var fileInfo = new TrustFileInfo
        {
            Size = (uint)Marshal.SizeOf<TrustFileInfo>(),
            Path = Path.GetFullPath(path)
        };
        var filePointer = Marshal.AllocHGlobal(Marshal.SizeOf<TrustFileInfo>());
        Marshal.StructureToPtr(fileInfo, filePointer, false);
        var data = new TrustData
        {
            Size = (uint)Marshal.SizeOf<TrustData>(),
            UiChoice = 2, // WTD_UI_NONE
            UnionChoice = 1, // WTD_CHOICE_FILE
            File = filePointer,
            StateAction = 1, // WTD_STATEACTION_VERIFY
            ProviderFlags = 0x80 // Check revocation for the chain excluding the root.
        };
        var action = new Guid("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");
        try
        {
            var status = WinVerifyTrust(IntPtr.Zero, ref action, ref data);
            if (status != 0)
                throw new CryptographicException($"The WebView2 installer signature is not trusted (0x{status:X8}).");
            using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
            if (certificate.GetNameInfo(X509NameType.SimpleName, false) != "Microsoft Corporation")
                throw new CryptographicException("The WebView2 installer was not signed by Microsoft.");
        }
        finally
        {
            data.StateAction = 2; // WTD_STATEACTION_CLOSE
            WinVerifyTrust(IntPtr.Zero, ref action, ref data);
            Marshal.DestroyStructure<TrustFileInfo>(filePointer);
            Marshal.FreeHGlobal(filePointer);
        }
    }

    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref TrustData data);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct TrustFileInfo
    {
        public uint Size;
        [MarshalAs(UnmanagedType.LPWStr)] public string Path;
        public IntPtr Handle;
        public IntPtr KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TrustData
    {
        public uint Size;
        public IntPtr PolicyCallback;
        public IntPtr SipClientData;
        public uint UiChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr File;
        public uint StateAction;
        public IntPtr StateData;
        public IntPtr UrlReference;
        public uint ProviderFlags;
        public uint UiContext;
        public IntPtr SignatureSettings;
    }
}
