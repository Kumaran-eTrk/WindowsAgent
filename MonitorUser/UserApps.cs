using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MontiorUserStandlone
{
    using Serilog;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Security.Principal;
    using System.Text;

    public class OpenedApplicationsFetcher
    {
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool EnumDesktopWindows(IntPtr hDesktop, EnumWindowsProc ewp, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

         [DllImport("psapi.dll")]
       private static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, StringBuilder lpBaseName, int nSize);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);
        [DllImport("user32.dll", SetLastError = true)]
         private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern int GetWindowModuleFileNameW(IntPtr hwnd, StringBuilder lpszFileName, int cchFileNameMax);


        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(IntPtr TokenHandle, TOKEN_INFORMATION_CLASS TokenInformationClass, IntPtr TokenInformation, uint TokenInformationLength, out uint ReturnLength);

        private enum TOKEN_INFORMATION_CLASS
        {
            TokenUser = 1
        }

        [DllImport("kernel32.dll")]
        public static extern bool ProcessIdToSessionId(uint dwProcessId, out uint pSessionId);

        public static uint GetCurrentSessionId()
        {
            uint sessionId;
            if (!ProcessIdToSessionId((uint)Process.GetCurrentProcess().Id, out sessionId))
            {
                throw new Exception("Failed to get the session ID.");
            }
            return sessionId;
        }

        public static List<string> GetProcessesInCurrentSession()
        {
            var processesInSession = new List<string>();
            uint currentSessionId = GetCurrentSessionId();
            Log.Information("Current Session ID :  " + currentSessionId);

            foreach (var process in Process.GetProcesses())
            {
                uint processSessionId;
                if (ProcessIdToSessionId((uint)process.Id, out processSessionId) && processSessionId == currentSessionId &&
                                    process.MainWindowHandle != IntPtr.Zero)
                {
                    Log.Information("GetProcesinCurrentsession : " +  process.MainWindowTitle + " : >>>> " + process.ProcessName);
                    processesInSession.Add(process.ProcessName); 
                }
            }

            return processesInSession;
        }

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        private const uint PROCESS_QUERY_INFORMATION = 0x0400;

        public static string GetProcessOwner(int processId)
        {
            IntPtr processHandle = IntPtr.Zero;
            IntPtr tokenHandle = IntPtr.Zero;

            try
            {
                // processHandle = Process.GetProcessById(processId).Handle;
                processHandle = OpenProcess(PROCESS_QUERY_INFORMATION, false, processId);

                if (processHandle == IntPtr.Zero)
                {
                    Log.Information("Process Handle 0");
                    return null;
                }

                if (!OpenProcessToken(processHandle, 8, out tokenHandle))
                {
                    Log.Information("Process Handle 8");
                    CloseHandle(processHandle);
                    return null;
                }

                uint tokenInfoLength = 0;
                GetTokenInformation(tokenHandle, TOKEN_INFORMATION_CLASS.TokenUser, IntPtr.Zero, 0, out tokenInfoLength);
                IntPtr tokenInfo = Marshal.AllocHGlobal((int)tokenInfoLength);

                if (!GetTokenInformation(tokenHandle, TOKEN_INFORMATION_CLASS.TokenUser, tokenInfo, tokenInfoLength, out tokenInfoLength))
                {
                    return null;
                }

                WindowsIdentity identity = new WindowsIdentity(tokenInfo);
                Log.Information("Successful Identity Name : " + identity.Name);
                return identity.Name;
            }
            catch(Exception ex)
            {
                Log.Information("Within GetProcessOwner Exception ---->" + ex.StackTrace.ToString());
            }
            finally
            {
                if (tokenHandle != IntPtr.Zero)
                {
                    CloseHandle(tokenHandle);
                }
            }
            return null;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        public static List<string> GGetOpenedWindowsTitles()
        {
            var processes = new List<string>();
            var currentUserName = Environment.UserName;
            Log.Information("Current Username : " +  currentUserName);
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    Log.Information("Process Owner : " + GetProcessOwner(process.Id));
                    if (GetProcessOwner(process.Id) == currentUserName)
                    {
                        Log.Information("GetOpenedWindowsTitles ProcessName : " + process.ProcessName);
                        processes.Add(process.ProcessName);
                    }
                }
                catch
                {
                    // Exception handling if needed
                }
            }

            return processes;
        }

        public static List<string> GetOpenedWindowsTitles()
        {
            List<string> windowTitles = new List<string>();
            EnumDesktopWindows(IntPtr.Zero, new EnumWindowsProc((hWnd, lParam) =>
            {
                if (IsWindowVisible(hWnd))
                {
                    StringBuilder sbTitle = new StringBuilder(1024);
                    GetWindowText(hWnd, sbTitle, sbTitle.Capacity);
                    if (!string.IsNullOrWhiteSpace(sbTitle.ToString()))
                    {
                        windowTitles.Add(sbTitle.ToString());
                       
                    }
                }
                
                return true;
            }), IntPtr.Zero);
            // string readableWindowTitles = string.Join(", ", windowTitles);

            //      Console.WriteLine("Windows Titles: " + readableWindowTitles);
           
           
            return windowTitles;
        }


public static List<string> GetOpenedWindowsModuleNames()
{
    List<string> moduleNames = new List<string>();

    EnumDesktopWindows(IntPtr.Zero, new EnumWindowsProc((hWnd, lParam) =>
    {
        if (IsWindowVisible(hWnd))
        {
            uint processId;
            GetWindowThreadProcessId(hWnd, out processId);
            Process process = Process.GetProcessById((int)processId);
            
            try
            {
                string moduleName = process.MainModule.ModuleName;
                moduleNames.Add(moduleName);
            }
            catch (Exception ex)
            {
                Log.Information($"Error getting module name for process {process.ProcessName}: {ex.Message}");
            }
        }
        return true;
    }), IntPtr.Zero);

    string readableModuleNames = string.Join(", ", moduleNames);
    Log.Information("Module Names: " + readableModuleNames);

    return moduleNames;
}



    


        public static string GetFocusedWindowTitle()
        {
            IntPtr focusedWindowHandle = GetForegroundWindow();
            StringBuilder sbTitle = new StringBuilder(1024);
            GetWindowText(focusedWindowHandle, sbTitle, sbTitle.Capacity);
            return sbTitle.ToString();
        }


     
      /*  public static void Main()
        {
            Console.WriteLine("Opened windows titles:");
            foreach (var title in GetOpenedWindowsTitles())
            {
                Console.WriteLine(title);
            }

            Console.WriteLine("\nFocused window title:");
            Console.WriteLine(GetFocusedWindowTitle());
        }*/
    }
}
