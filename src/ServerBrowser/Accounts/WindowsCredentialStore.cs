using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace ServerBrowser.Accounts;

public sealed class WindowsCredentialStore : ISecretStore {
    private const uint CredentialTypeGeneric = 1;
    private const uint CredentialPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;
    private readonly string _targetPrefix;

    public WindowsCredentialStore(string targetPrefix) {
        _targetPrefix = targetPrefix;
    }

    /// <summary>
    /// Probes the credential API with a harmless read. Wine exports these functions, so this
    /// stays true under Proton; it only turns false on hosts that lack them entirely.
    /// </summary>
    public static bool IsAvailable() {
        if (!OperatingSystem.IsWindows()) return false;

        try {
            if (CredRead($"ServerBrowser.Probe/{Guid.NewGuid():N}", CredentialTypeGeneric, 0, out var pointer)) {
                CredFree(pointer);
            }
            return true;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException) {
            return false;
        }
    }

    public void Write(string accountId, string password) {
        var bytes = Encoding.Unicode.GetBytes(password);
        var blob = Marshal.AllocHGlobal(bytes.Length);
        try {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var credential = new NativeCredential {
                Type = CredentialTypeGeneric,
                TargetName = Target(accountId),
                CredentialBlobSize = (uint)bytes.Length,
                CredentialBlob = blob,
                Persist = CredentialPersistLocalMachine,
                UserName = accountId
            };
            if (!CredWrite(ref credential, 0)) {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        finally {
            for (var i = 0; i < bytes.Length; i++) Marshal.WriteByte(blob, i, 0);
            Marshal.FreeHGlobal(blob);
            Array.Clear(bytes);
        }
    }

    public string? Read(string accountId) {
        if (!CredRead(Target(accountId), CredentialTypeGeneric, 0, out var pointer)) {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound) return null;
            throw new Win32Exception(error);
        }

        try {
            var credential = Marshal.PtrToStructure<NativeCredential>(pointer);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0) return "";
            var bytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
            try {
                return Encoding.Unicode.GetString(bytes);
            }
            finally {
                Array.Clear(bytes);
            }
        }
        finally {
            CredFree(pointer);
        }
    }

    public void Delete(string accountId) {
        if (CredDelete(Target(accountId), CredentialTypeGeneric, 0)) return;
        var error = Marshal.GetLastWin32Error();
        if (error != ErrorNotFound) throw new Win32Exception(error);
    }

    private string Target(string accountId) => $"{_targetPrefix}/{accountId}";

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential {
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

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref NativeCredential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}
