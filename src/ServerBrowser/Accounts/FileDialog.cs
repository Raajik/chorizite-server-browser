using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace ServerBrowser.Accounts;

/// <summary>
/// Native Windows file-open dialog via comdlg32's GetOpenFileNameW. The modern
/// IFileOpenDialog COM API is unusable from a Chorizite plugin: CLR COM interop
/// fails inside the collectible AssemblyLoadContext ("Typelib export" 0x80131165),
/// and raw vtable invocation of the shell dialog returns null results (the stock
/// dialog's GetResult slot misbehaves in this host). The classic dialog is a
/// single documented P/Invoke with no COM at all.
/// </summary>
internal static class FileDialog {
    /// <summary>Optional logger injected by the plugin for diagnostics.</summary>
    internal static Action<string>? Log;

    private const uint OFN_FILEMUSTEXIST = 0x00001000;
    private const uint OFN_PATHMUSTEXIST = 0x00000800;
    private const uint OFN_HIDEREADONLY = 0x00000004;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        // lpstrFilter: pairs of NUL-terminated strings, double-NUL terminated.
        public string lpstrFilter;
        public string? lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public IntPtr lpstrFileTitle;
        public int nMaxFileTitle;
        public string? lpstrInitialDir;
        public string lpstrTitle;
        public uint flags;
        // remainder of the struct is not needed; marshal the prefix only via
        // explicit size below.
        public ushort nFileOffset;
        public ushort nFileExtension;
        public string? lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public IntPtr lpTemplateName;
        public IntPtr pvReserved;
        public uint dwReserved;
        public uint flagsEx;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetOpenFileNameW(ref OpenFileName ofn);

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode)]
    private static extern int CommDlgExtendedError();

    public static string? PickExecutable() {
        if (!OperatingSystem.IsWindows()) return null;
        try {
            // GetOpenFileNameW is thread-agnostic, but keep the STA thread for
            // parity with the modal behavior the UI expects.
            string? result = null;
            Exception? failure = null;
            var thread = new Thread(() => {
                try {
                    result = PickExecutableCore();
                }
                catch (Exception ex) {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure is not null) {
                Log?.Invoke($"FileDialog failure: {failure.GetType().Name}: {failure.Message}");
                throw new InvalidOperationException("File dialog failed", failure);
            }
            return result;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) {
            return null;
        }
    }

    private static string? PickExecutableCore() {
        const int maxFile = 32768;
        var fileBuffer = Marshal.AllocCoTaskMem(maxFile * 2);
        Marshal.WriteInt16(fileBuffer, 0, 0); // empty initial filename
        try {
            var ofn = new OpenFileName {
                lStructSize = Marshal.SizeOf<OpenFileName>(),
                hwndOwner = IntPtr.Zero,
                hInstance = IntPtr.Zero,
                lpstrFilter = "Executables (*.exe)\0*.exe\0All files (*.*)\0*.*\0",
                lpstrCustomFilter = null,
                nMaxCustFilter = 0,
                nFilterIndex = 1,
                lpstrFile = fileBuffer,
                nMaxFile = maxFile,
                lpstrFileTitle = IntPtr.Zero,
                nMaxFileTitle = 0,
                lpstrInitialDir = null,
                lpstrTitle = "Select alternate client executable",
                flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_HIDEREADONLY,
                lpstrDefExt = null
            };

            var ok = GetOpenFileNameW(ref ofn);
            if (!ok) {
                var err = CommDlgExtendedError();
                // 0 = user cancelled; anything else is a real error worth logging.
                if (err != 0) Log?.Invoke($"GetOpenFileNameW failed with CommDlgExtendedError={err}");
                return null;
            }

            var path = Marshal.PtrToStringUni(fileBuffer);
            Log?.Invoke($"GetOpenFileNameW picked: {path}");
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }
        finally {
            Marshal.FreeCoTaskMem(fileBuffer);
        }
    }
}
