using System.Runtime.InteropServices;
using System.Text;

namespace StoneshardCompanion;

public static class Native
{
    public delegate bool WindowVisitor(nint hwnd,nint data);
    [DllImport("user32")] public static extern bool EnumWindows(WindowVisitor visitor,nint data);
    [DllImport("user32")] public static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32",CharSet=CharSet.Unicode)] public static extern int GetWindowText(nint hwnd,StringBuilder text,int count);
    public const uint Message = 0x8000 + 0x437, Magic = 0x53484331;
    [DllImport("kernel32", SetLastError = true)] public static extern nint OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32", SetLastError = true)] public static extern bool CloseHandle(nint handle);
    [DllImport("kernel32", SetLastError = true)] public static extern nint VirtualAllocEx(nint process, nint address, nuint size, uint type, uint protect);
    [DllImport("kernel32", SetLastError = true)] public static extern bool VirtualFreeEx(nint process, nint address, nuint size, uint type);
    [DllImport("kernel32", SetLastError = true)] public static extern bool WriteProcessMemory(nint process, nint address, byte[] bytes, nuint size, out nuint written);
    [DllImport("kernel32", SetLastError = true)] public static extern nint CreateRemoteThread(nint process, nint attributes, nuint stack, nint start, nint parameter, uint flags, nint id);
    [DllImport("kernel32", SetLastError = true)] public static extern uint WaitForSingleObject(nint handle, uint timeout);
    [DllImport("kernel32", SetLastError = true)] public static extern bool GetExitCodeThread(nint thread, out uint code);
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] public static extern nint LoadLibraryEx(string file, nint fileHandle, uint flags);
    [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true)] public static extern nint GetProcAddress(nint module, string name);
    [DllImport("kernel32", CharSet = CharSet.Unicode)] public static extern nint GetModuleHandle(string name);
    [DllImport("kernel32")] public static extern bool FreeLibrary(nint module);
    [DllImport("user32", SetLastError = true)] public static extern bool PostMessage(nint hwnd, uint message, nuint wp, nint lp);
    [DllImport("user32")] public static extern nint GetForegroundWindow();
    [DllImport("user32")] public static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32")] public static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32")] public static extern bool IsIconic(nint hwnd);
    [DllImport("user32")] public static extern bool IsWindow(nint hwnd);
    [DllImport("user32")] public static extern nint GetWindow(nint hwnd,uint command);
    [DllImport("user32")] public static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32")] public static extern bool GetClientRect(nint hwnd, out Rect rect);
    [DllImport("user32")] public static extern bool ClientToScreen(nint hwnd, ref Point point);
    [DllImport("user32")] public static extern bool GetCursorPos(out Point point);
    [DllImport("user32", EntryPoint = "GetWindowLongPtrW")] public static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32", EntryPoint = "SetWindowLongPtrW")] public static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
    [DllImport("user32")] public static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32", SetLastError=true)] public static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32")] public static extern bool UnregisterHotKey(nint hwnd, int id);
    [DllImport("user32")] public static extern short GetAsyncKeyState(int key);
    [DllImport("user32")] public static extern nint MonitorFromWindow(nint hwnd, uint flags);
    [DllImport("user32",CharSet=CharSet.Unicode)] public static extern bool GetMonitorInfo(nint monitor,ref MonitorInfo info);
    [DllImport("user32")] public static extern uint GetDpiForWindow(nint hwnd);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X,Y; }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] public struct MonitorInfo { public int Size; public Rect Monitor,Work;public uint Flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Device; }
}
