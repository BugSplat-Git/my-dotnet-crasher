using BugSplatDotNetStandard;
using System.Runtime.InteropServices;
using System.Diagnostics;

public class Program
{
    private static BugSplat bugsplat = new BugSplat("fred", "MyDotnetCrasher", "1.0.0");

    // P/Invoke declarations for minidump creation
    [DllImport("dbghelp.dll", SetLastError = true)]
    private static extern bool MiniDumpWriteDump(
        IntPtr hProcess,
        uint processId,
        IntPtr hFile,
        uint dumpType,
        IntPtr exceptionParam,
        IntPtr userStreamParam,
        IntPtr callbackParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint CREATE_ALWAYS = 2;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;
    private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

    // Minidump types
    private enum MINIDUMP_TYPE : uint
    {
        MiniDumpNormal = 0x00000000,
        MiniDumpWithDataSegs = 0x00000001,
        MiniDumpWithFullMemory = 0x00000002,
        MiniDumpWithHandleData = 0x00000004,
        MiniDumpFilterMemory = 0x00000008,
        MiniDumpScanMemory = 0x00000010,
        MiniDumpWithUnloadedModules = 0x00000020,
        MiniDumpWithIndirectlyReferencedMemory = 0x00000040,
        MiniDumpFilterModulePaths = 0x00000080,
        MiniDumpWithProcessThreadData = 0x00000100,
        MiniDumpWithPrivateReadWriteMemory = 0x00000200,
        MiniDumpWithoutOptionalData = 0x00000400,
        MiniDumpWithFullMemoryInfo = 0x00000800,
        MiniDumpWithThreadInfo = 0x00001000,
        MiniDumpWithCodeSegs = 0x00002000,
        MiniDumpWithoutAuxiliaryState = 0x00004000,
        MiniDumpWithFullAuxiliaryState = 0x00008000,
        MiniDumpWithPrivateWriteCopyMemory = 0x00010000,
        MiniDumpIgnoreInaccessibleMemory = 0x00020000,
        MiniDumpWithTokenInformation = 0x00040000
    }

    public static void Main(string[] args)
    {
        // Set up global exception handlers
        SetupGlobalExceptionHandling();

        // Your application logic
        throw new Exception("Test exception for BugSplat reporting");
    }

    private static void SetupGlobalExceptionHandling()
    {
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        try
        {
            var exception = e.ExceptionObject as Exception;
            if (exception != null)
            {
                // Log exception details
                Console.WriteLine($"Exception Type: {exception.GetType().FullName}");
                Console.WriteLine($"Message: {exception.Message}");
                Console.WriteLine($"Stack Trace: {exception.StackTrace}");
                
                // Generate minidump
                var minidumpPath = CreateMinidump();
                
                if (!string.IsNullOrEmpty(minidumpPath))
                {
                    // Create FileInfo and post to BugSplat with the minidump
                    var options = new MinidumpPostOptions()
                    {
                        MinidumpType = BugSplat.MinidumpTypeId.DotNet
                    };
                    FileInfo minidumpFile = new FileInfo(minidumpPath);
                    Task.Run(() => bugsplat.Post(minidumpFile, options)).Wait();
                }
                else
                {
                    // Fallback to posting without minidump
                    Task.Run(() => bugsplat.Post(exception)).Wait();
                }
            }
        }
        catch (Exception reportingException)
        {
            // Don't let reporting exceptions crash the app
            Console.Error.WriteLine($"Failed to report exception: {reportingException}");
        }
        finally
        {
            // For console apps, exit after reporting
            if (e.IsTerminating)
            {
                Environment.Exit(1);
            }
        }
    }

    private static string CreateMinidump()
    {
        string minidumpPath = Path.Combine(Path.GetTempPath(), $"crash_{DateTime.Now:yyyyMMdd_HHmmss}_{Process.GetCurrentProcess().Id}.net64dmp");
        
        try
        {
            IntPtr fileHandle = CreateFile(
                minidumpPath,
                GENERIC_WRITE,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                CREATE_ALWAYS,
                FILE_ATTRIBUTE_NORMAL,
                IntPtr.Zero);

            if (fileHandle == INVALID_HANDLE_VALUE)
            {
                Console.Error.WriteLine($"Failed to create minidump file: {Marshal.GetLastWin32Error()}");
                return null;
            }

            try
            {
                Process currentProcess = Process.GetCurrentProcess();

                // Use lighter-weight flags optimized for C# debugging with line number support
                // These provide good managed debugging without full memory dump
                uint dumpType = (uint)(
                    MINIDUMP_TYPE.MiniDumpWithDataSegs |                    // Include data segments
                    MINIDUMP_TYPE.MiniDumpWithHandleData |                  // Include handle info
                    MINIDUMP_TYPE.MiniDumpWithThreadInfo |                  // Include thread info
                    MINIDUMP_TYPE.MiniDumpWithUnloadedModules |             // Include unloaded modules
                    MINIDUMP_TYPE.MiniDumpWithFullMemoryInfo |              // Memory info (not full memory)
                    MINIDUMP_TYPE.MiniDumpWithProcessThreadData |           // Process thread data
                    MINIDUMP_TYPE.MiniDumpWithIndirectlyReferencedMemory |  // Referenced memory
                    MINIDUMP_TYPE.MiniDumpIgnoreInaccessibleMemory        // Skip inaccessible memory
                );

                bool success = MiniDumpWriteDump(
                    currentProcess.Handle,
                    (uint)currentProcess.Id,
                    fileHandle,
                    dumpType,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero);

                if (!success)
                {
                    int error = Marshal.GetLastWin32Error();
                    Console.Error.WriteLine($"MiniDumpWriteDump failed with error code: {error} (0x{error:X})");
                    return null;
                }

                var fileInfo = new FileInfo(minidumpPath);
                Console.WriteLine($"Minidump created: {minidumpPath}");
                Console.WriteLine($"Minidump size: {fileInfo.Length:N0} bytes ({fileInfo.Length / 1024.0:N2} KB)");
                return minidumpPath;
            }
            finally
            {
                CloseHandle(fileHandle);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Exception creating minidump: {ex.Message}");
            return null;
        }
    }

    private static async void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        try
        {
            await bugsplat.Post(e.Exception);
            e.SetObserved(); // Prevent process termination
        }
        catch (Exception reportingException)
        {
            Console.Error.WriteLine($"Failed to report task exception: {reportingException}");
        }
    }
}