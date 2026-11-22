using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace EZsound.Helpers;

/// <summary>
/// Safe helper for retrieving process names and executable paths across 32-bit and 64-bit boundaries.
/// Prevents "Access is denied" exceptions when reading process metadata.
/// </summary>
public static class ProcessHelper
{
    private const uint ProcessQueryLimitedInformation = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr hProcess, int flags, [Out] StringBuilder lpExeName, ref int lpdwSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    /// <summary>
    /// Gets the full executable path for a process ID using both .NET API and Win32 QueryFullProcessImageName fallback.
    /// </summary>
    public static string GetExecutablePath(uint processId)
    {
        if (processId == 0) return string.Empty;

        // 1. Try Process.MainModule (standard .NET)
        try
        {
            using var proc = Process.GetProcessById((int)processId);
            var path = proc.MainModule?.FileName;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                return path;
            }
        }
        catch
        {
            // Process.MainModule commonly throws Win32Exception (Access Denied)
            // for elevated processes, 32-to-64 bit processes, or during initialization.
        }

        // 2. Fallback to OpenProcess with PROCESS_QUERY_LIMITED_INFORMATION
        // This succeeds for almost all user processes without requiring administrative privileges.
        try
        {
            IntPtr hProcess = OpenProcess(ProcessQueryLimitedInformation, false, (int)processId);
            if (hProcess != IntPtr.Zero)
            {
                try
                {
                    var sb = new StringBuilder(1024);
                    int size = sb.Capacity;
                    if (QueryFullProcessImageName(hProcess, 0, sb, ref size))
                    {
                        var path = sb.ToString();
                        if (File.Exists(path))
                        {
                            return path;
                        }
                    }
                }
                finally
                {
                    CloseHandle(hProcess);
                }
            }
        }
        catch (Exception ex)
        {
            // Expected: process may have exited or access denied for elevated/system processes.
            App.Log($"[ProcessHelper] Could not read path for PID {processId}: {ex.Message}");
        }

        return string.Empty;
    }

    /// <summary>
    /// Gets a human-readable process name for a given process ID.
    /// </summary>
    public static string GetProcessName(uint processId, string? fallbackPath = null)
    {
        if (processId == 0) return string.Empty;

        try
        {
            using var proc = Process.GetProcessById((int)processId);
            var name = proc.ProcessName;
            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }
        }
        catch (ArgumentException)
        {
            // Expected: process has already terminated by the time we query it.
        }

        if (!string.IsNullOrEmpty(fallbackPath))
        {
            try
            {
                var name = Path.GetFileNameWithoutExtension(fallbackPath);
                if (!string.IsNullOrEmpty(name))
                {
                    return name;
                }
            }
            catch (ArgumentException)
            {
                // Expected: fallback path may contain invalid path characters from raw session titles.
            }
        }

        return string.Empty;
    }
}
