using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DTDTrainer;

/// <summary>
/// Quản lý việc chèn DTDLoader.dll trực tiếp vào tiến trình game DaveTheDiver.exe.
/// Tự động nạp khi game đang chạy hoặc vừa khởi động, không cần bất kỳ file nào trong thư mục game.
/// </summary>
public static class Injector
{
    const string ProcessName = "DaveTheDiver";
    const string LoaderDllName = "DTDLoader.dll";

    #region Win32 P/Invoke

    [Flags]
    enum ProcessAccessFlags : uint
    {
        CreateThread = 0x0002,
        SetSessionId = 0x0004,
        VmOperation = 0x0008,
        VmRead = 0x0010,
        VmWrite = 0x0020,
        DupHandle = 0x0040,
        CreateProcess = 0x0080,
        SetQuota = 0x0100,
        SetInformation = 0x0200,
        QueryInformation = 0x0400,
        SuspendResume = 0x0800,
        QueryLimitedInformation = 0x1000,
        Synchronize = 0x00100000,
        AllAccess = 0x001F0FFF
    }

    const uint MEM_COMMIT = 0x00001000;
    const uint MEM_RESERVE = 0x00002000;
    const uint MEM_RELEASE = 0x00008000;
    const uint PAGE_READWRITE = 0x04;
    const uint WAIT_OBJECT_0 = 0x00000000;
    const uint INFINITE = 0xFFFFFFFF;

    const uint TH32CS_SNAPMODULE = 0x00000008;
    const uint TH32CS_SNAPMODULE32 = 0x00000010;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct MODULEENTRY32
    {
        public uint dwSize;
        public uint th32ModuleID;
        public uint th32ProcessID;
        public uint GlblcntUsage;
        public uint ProccntUsage;
        public IntPtr modBaseAddr;
        public uint modBaseSize;
        public IntPtr hModule;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szModule;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExePath;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr OpenProcess(ProcessAccessFlags processAccess, bool bInheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, nuint dwSize, uint flAllocationType, uint flProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, nuint nSize, out nuint lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, nuint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out uint lpThreadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetExitCodeThread(IntPtr hThread, out uint lpExitCode);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, nuint dwSize, uint dwFreeType);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
    static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern bool Module32First(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern bool Module32Next(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

    #endregion

    /// <summary>Lấy tiến trình game DaveTheDiver đang chạy (nếu có).</summary>
    public static Process GetGameProcess()
    {
        var procs = Process.GetProcessesByName(ProcessName);
        return procs.Length > 0 ? procs[0] : null;
    }

    /// <summary>Kiểm tra xem module DLL đã được nạp trong tiến trình mục tiêu hay chưa.</summary>
    public static bool IsModuleLoaded(int processId, string moduleName)
    {
        IntPtr snap = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, (uint)processId);
        if (snap == IntPtr.Zero || snap == (IntPtr)(-1))
            return false;

        try
        {
            var me = new MODULEENTRY32 { dwSize = (uint)Marshal.SizeOf<MODULEENTRY32>() };
            if (Module32First(snap, ref me))
            {
                do
                {
                    if (string.Equals(me.szModule, moduleName, StringComparison.OrdinalIgnoreCase))
                        return true;
                } while (Module32Next(snap, ref me));
            }
        }
        catch { }
        finally
        {
            CloseHandle(snap);
        }

        return false;
    }

    /// <summary>Đường dẫn đầy đủ tới DTDLoader.dll trong Tool-DTD\runtime.</summary>
    public static string LoaderDllPath => Path.Combine(ModInstaller.RuntimeDir, LoaderDllName);

    /// <summary>
    /// Chèn DLL vào tiến trình đích thông qua CreateRemoteThread + LoadLibraryW.
    /// </summary>
    public static (bool Success, string Message) InjectDll(int processId, string dllPath)
    {
        if (!File.Exists(dllPath))
            return (false, L.T($"Không tìm thấy file: {dllPath}", $"File not found: {dllPath}"));

        string fullDllPath = Path.GetFullPath(dllPath);

        if (IsModuleLoaded(processId, Path.GetFileName(fullDllPath)))
            return (true, L.T("Mod đã được nạp sẵn trong game.", "Mod is already loaded in the game."));

        ProcessAccessFlags desiredAccess = ProcessAccessFlags.CreateThread |
                                           ProcessAccessFlags.QueryInformation |
                                           ProcessAccessFlags.VmOperation |
                                           ProcessAccessFlags.VmWrite |
                                           ProcessAccessFlags.VmRead;

        IntPtr hProcess = OpenProcess(desiredAccess, false, processId);
        if (hProcess == IntPtr.Zero)
        {
            // Thử lại với AllAccess
            hProcess = OpenProcess(ProcessAccessFlags.AllAccess, false, processId);
            if (hProcess == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                return (false, L.T($"Không thể mở tiến trình game (Mã lỗi {err}). Thử chạy tool với quyền Admin.",
                                  $"Cannot open game process (Win32 error {err}). Try running the trainer as Administrator."));
            }
        }

        IntPtr pRemoteMem = IntPtr.Zero;
        IntPtr hThread = IntPtr.Zero;

        try
        {
            byte[] bytes = Encoding.Unicode.GetBytes(fullDllPath + "\0");
            nuint size = (nuint)bytes.Length;

            pRemoteMem = VirtualAllocEx(hProcess, IntPtr.Zero, size, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            if (pRemoteMem == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                return (false, L.T($"Không thể cấp phát bộ nhớ trong game (Mã lỗi {err}).",
                                  $"Failed to allocate memory in game process (Error {err})."));
            }

            if (!WriteProcessMemory(hProcess, pRemoteMem, bytes, size, out _))
            {
                int err = Marshal.GetLastWin32Error();
                return (false, L.T($"Không thể ghi đường dẫn DLL vào game (Mã lỗi {err}).",
                                  $"Failed to write memory to game process (Error {err})."));
            }

            IntPtr hKernel32 = GetModuleHandle("kernel32.dll");
            IntPtr pLoadLibraryW = GetProcAddress(hKernel32, "LoadLibraryW");
            if (pLoadLibraryW == IntPtr.Zero)
                return (false, L.T("Không tìm thấy hàm LoadLibraryW.", "Cannot find LoadLibraryW address."));

            hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, pLoadLibraryW, pRemoteMem, 0, out _);
            if (hThread == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                return (false, L.T($"Không thể tạo luồng nạp DLL trong game (Mã lỗi {err}).",
                                  $"Failed to create remote thread (Error {err})."));
            }

            uint waitResult = WaitForSingleObject(hThread, 5000);
            if (waitResult == WAIT_OBJECT_0)
            {
                GetExitCodeThread(hThread, out uint exitCode);
                if (exitCode == 0)
                {
                    return (false, L.T("Game từ chối nạp DLL (LoadLibrary trả về 0). Kiểm tra lại file DTDLoader.dll.",
                                      "Game rejected loading DLL (LoadLibrary returned 0)."));
                }
            }

            return (true, L.T("Đã chèn mod vào tiến trình game thành công!", "Successfully injected mod into game process!"));
        }
        finally
        {
            if (pRemoteMem != IntPtr.Zero)
                VirtualFreeEx(hProcess, pRemoteMem, 0, MEM_RELEASE);
            if (hThread != IntPtr.Zero)
                CloseHandle(hThread);
            if (hProcess != IntPtr.Zero)
                CloseHandle(hProcess);
        }
    }

    /// <summary>
    /// Tự động dò tiến trình game và chèn DTDLoader.dll nếu chưa nạp.
    /// </summary>
    public static (bool Injected, string Message) TryInject()
    {
        using var proc = GetGameProcess();
        if (proc == null)
            return (false, L.T("Game chưa chạy.", "Game is not running."));

        string loader = LoaderDllPath;
        if (!File.Exists(loader))
            return (false, L.T($"Thiếu bộ nạp mod: {loader}", $"Missing loader DLL: {loader}"));

        if (IsModuleLoaded(proc.Id, LoaderDllName))
            return (true, L.T("Mod đã được nạp trong game.", "Mod is already loaded in game."));

        return InjectDll(proc.Id, loader);
    }
}
