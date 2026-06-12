using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace ImagePilot.Api.Services;

public sealed class WindowsCredentialStore
{
    private const string SqlPasswordTarget = "ImagePilot_Agent/SqlServerPassword";
    private const string PromptAiApiKeyTarget = "ImagePilot_Agent/PromptAiApiKey";
    private const int GenericCredential = 1;
    private const int PersistLocalMachine = 2;

    public void SaveSqlPassword(string password)
    {
        SaveSecret(SqlPasswordTarget, password);
    }

    public string? ReadSqlPassword()
    {
        return ReadSecret(SqlPasswordTarget);
    }

    public void SavePromptAiApiKey(string apiKey)
    {
        SaveSecret(PromptAiApiKeyTarget, apiKey);
    }

    public string? ReadPromptAiApiKey()
    {
        return ReadSecret(PromptAiApiKeyTarget);
    }

    private static void SaveSecret(string targetName, string secret)
    {
        var bytes = Encoding.Unicode.GetBytes(secret);
        if (bytes.Length > 5120)
        {
            throw new ArgumentException("Secret is too long.", nameof(secret));
        }

        var credential = new NativeCredential
        {
            Type = GenericCredential,
            TargetName = targetName,
            CredentialBlobSize = (uint)bytes.Length,
            CredentialBlob = Marshal.StringToCoTaskMemUni(secret),
            Persist = PersistLocalMachine,
            UserName = Environment.UserName
        };

        try
        {
            if (!CredWrite(ref credential, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        finally
        {
            Marshal.ZeroFreeCoTaskMemUnicode(credential.CredentialBlob);
        }
    }

    private static string? ReadSecret(string targetName)
    {
        if (!CredRead(targetName, GenericCredential, 0, out var pointer))
        {
            return null;
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(pointer);
            return credential.CredentialBlob == IntPtr.Zero
                ? null
                : Marshal.PtrToStringUni(credential.CredentialBlob, (int)credential.CredentialBlobSize / 2);
        }
        finally
        {
            CredFree(pointer);
        }
    }

    [DllImport("advapi32", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite([In] ref NativeCredential userCredential, [In] uint flags);

    [DllImport("advapi32", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32", EntryPoint = "CredFree", SetLastError = true)]
    private static extern void CredFree([In] IntPtr cred);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string UserName;
    }
}
