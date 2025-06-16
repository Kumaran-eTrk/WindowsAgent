using System.CodeDom;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Timers;
using Serilog;

namespace MontiorUserStandlone
{
    public class WindowsAccess
    {
        public WindowsAccess() { }

        [StructLayout(LayoutKind.Sequential)]
        public struct WTS_SESSION_INFO
        {
            public int SessionID;

            [MarshalAs(UnmanagedType.LPStr)]
            public string pWinStationName;
            public int State;
        }

        [DllImport("Wtsapi32.dll")]
        public static extern int WTSGetActiveConsoleSessionId();

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        static extern bool WTSQuerySessionInformation(
            IntPtr hServer,
            int sessionId,
            int wtsInfoClass,
            out IntPtr ppBuffer,
            out uint pBytesReturned
        );

        [DllImport("Wtsapi32.dll")]
        public static extern int WTSEnumerateSessions(
            IntPtr hServer,
            [MarshalAs(UnmanagedType.U4)] int Reserved,
            [MarshalAs(UnmanagedType.U4)] int Version,
            ref IntPtr ppSessionInfo,
            [MarshalAs(UnmanagedType.U4)] ref int pCount
        );

        [DllImport("wtsapi32.dll", ExactSpelling = true, SetLastError = false)]
        public static extern void WTSFreeMemory(IntPtr memory);

        [DllImport("kernel32.dll")]
        public static extern uint GetLastError();

        [StructLayout(LayoutKind.Sequential)]
        public struct WTSSessionInfoEx
        {
            public int Level;
            public int SessionId;
            public WTS_CONNECTSTATE_CLASS State;
            public int SessionFlags;
            public Luid LogonId;
            public long IdleTime;
            public long LogonTime;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool ExitWindowsEx(uint uFlags, uint dwReason);

        const uint EWX_LOGOFF = 0;
        const uint EWX_SHUTDOWN = 0x00000001;
        const uint EWX_REBOOT = 0x00000002;
        const uint EWX_FORCE = 0x00000004;
        const uint EWX_POWEROFF = 0x00000008;
        const uint EWX_FORCEIFHUNG = 0x00000010;

        [StructLayout(LayoutKind.Sequential)]
        public struct Luid
        {
            public uint LowPart;
            public int HighPart;
        }

        public enum WTS_INFO_CLASS
        {
            WTSInitialProgram = 0,
            WTSApplicationName = 1,
            WTSWorkingDirectory = 2,
            WTSOEMId = 3,
            WTSSessionId = 4,
            WTSUserName = 5,
            WTSWinStationName = 6,
            WTSDomainName = 7,
            WTSConnectState = 8,
            WTSClientBuildNumber = 9,
            WTSClientName = 10,
            WTSClientDirectory = 11,
            WTSClientProductId = 12,
            WTSClientHardwareId = 13,
            WTSClientAddress = 14,
            WTSClientDisplay = 15,
            WTSClientProtocolType = 16,
            WTSIdleTime = 17,
            WTSLogonTime = 18,
            WTSIncomingBytes = 19,
            WTSOutgoingBytes = 20,
            WTSIncomingFrames = 21,
            WTSOutgoingFrames = 22,
            WTSClientInfo = 23,
            WTSSessionInfo = 24,
            WTSSessionInfoEx = 25,
            WTSConfigInfo = 26,
            WTSValidationInfo = 27,
            WTSSessionAddressV4 = 28,
            WTSIsRemoteSession = 29
        }

        public static uint GetMySessionId()
        {
            IntPtr buffer;
            uint bytesReturned;
            uint mySessionId = 0;

            if (
                WTSQuerySessionInformation(
                    IntPtr.Zero,
                    -1,
                    (int)WTS_INFO_CLASS.WTSSessionId,
                    out buffer,
                    out bytesReturned
                )
            )
            {
                mySessionId = (uint)Marshal.ReadInt32(buffer);
                WTSFreeMemory(buffer);
            }
            return mySessionId;
        }

        public static bool ValidateActiveSession()
        {
            uint currentSessionId = (uint)WTSGetActiveConsoleSessionId();
            uint mySessionId = GetMySessionId();
            Log.Information($"Current Session {currentSessionId} Active Session {mySessionId}");
            return (currentSessionId == mySessionId);
        }

        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO
        {
            public static readonly int SizeOf = Marshal.SizeOf(typeof(LASTINPUTINFO));

            [MarshalAs(UnmanagedType.U4)]
            public uint cbSize;

            [MarshalAs(UnmanagedType.U4)]
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        public enum WTS_CONNECTSTATE_CLASS
        {
            WTSActive,
            WTSConnected,
            WTSConnectQuery,
            WTSShadow,
            WTSDisconnected,
            WTSIdle,
            WTSListen,
            WTSReset,
            WTSDown,
            WTSInit
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct WTSINFOA
        {
            public const int WINSTATIONNAME_LENGTH = 32;
            public const int DOMAIN_LENGTH = 17;
            public const int USERNAME_LENGTH = 20;
            public WTS_CONNECTSTATE_CLASS State;
            public int SessionId;
            public int IncomingBytes;
            public int OutgoingBytes;
            public int IncomingFrames;
            public int OutgoingFrames;
            public int IncomingCompressedBytes;
            public int OutgoingCompressedBytes;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = WINSTATIONNAME_LENGTH)]
            public byte[] WinStationNameRaw;
            public string WinStationName
            {
                get { return Encoding.ASCII.GetString(WinStationNameRaw); }
            }

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = DOMAIN_LENGTH)]
            public byte[] DomainRaw;
            public string Domain
            {
                get { return Encoding.ASCII.GetString(DomainRaw); }
            }

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = USERNAME_LENGTH + 1)]
            public byte[] UserNameRaw;
            public string UserName
            {
                get { return Encoding.ASCII.GetString(UserNameRaw); }
            }
            public long ConnectTimeUTC;
            public DateTime ConnectTime
            {
                get { return DateTime.FromFileTimeUtc(ConnectTimeUTC); }
            }
            public long DisconnectTimeUTC;
            public DateTime DisconnectTime
            {
                get { return DateTime.FromFileTimeUtc(DisconnectTimeUTC); }
            }
            public long LastInputTimeUTC;
            public DateTime LastInputTime
            {
                get { return DateTime.FromFileTimeUtc(LastInputTimeUTC); }
            }
            public long LogonTimeUTC;
            public DateTime LogonTime
            {
                get { return DateTime.FromFileTimeUtc(LogonTimeUTC); }
            }
            public long CurrentTimeUTC;
            public DateTime CurrentTime
            {
                get { return DateTime.FromFileTimeUtc(CurrentTimeUTC); }
            }
        }

        public static string? userName { get; set; }
        public static string? domainName { get; set; }
        public static string? machineName { get; set; }
        public static string? osVersion { get; set; }
        public static string? osType { get; set; }
        public static string? machineType { get; set; }
        public static DateTime logonDateTime { get; set; }
        public static uint idleTicks { get; set; }
        public static uint totalIdleTime { get; set; } = 0;
        public static uint idleTicksLast { get; set; } = 0;

        public static bool Is64BitOperatingSystem()
        {
            return IntPtr.Size == 8 || (IntPtr.Size == 4 && Is32BitProcessOn64BitProcessor());
        }

        public static uint GetLastActivity()
        {
            LASTINPUTINFO lastInputInfo = new LASTINPUTINFO
            {
                cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO))
            };

            if (!GetLastInputInfo(ref lastInputInfo))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            uint lastInputTick = lastInputInfo.dwTime;
            Log.Information(
                $"GetLastActivity - {Environment.TickCount} - {lastInputTick} = {Environment.TickCount - lastInputTick}"
            );
            uint idleTime = ((uint)Environment.TickCount - lastInputTick) / 1000; // Gets the idle time in seconds
            Log.Information($"GetLastActivity idleTime - {idleTime}");

            return idleTime; // Returns the idle time
        }

        public static bool GetLastActivityOld()
        {
            LASTINPUTINFO lastInputInfo = new LASTINPUTINFO();
            lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
            GetLastInputInfo(ref lastInputInfo);

            uint lastInputTick = lastInputInfo.dwTime;

            Log.Information($"GetLastActivity TickCount {Environment.TickCount} - {lastInputTick}");
            // Calculate the difference in ticks from when the last input event was received to now
            idleTicks = (uint)(Environment.TickCount - lastInputTick) / 1000;
            //Console.WriteLine("Idle Session " + idleTicks / 1000 + " seconds");
            Log.Information($"GetLastActivity - {idleTicks} - {idleTicksLast} - {totalIdleTime}");
            if (idleTicks > idleTicksLast)
            {
                idleTicksLast = idleTicks;
                if (totalIdleTime == 0)
                {
                    totalIdleTime = idleTicks;
                }
            }
            else if (idleTicks < idleTicksLast)
            {
                totalIdleTime += idleTicksLast + idleTicks;
                idleTicksLast = 0;
            }
            //totalIdleTime += idleTicks;
            return true;
        }

        private static bool Is32BitProcessOn64BitProcessor()
        {
            bool retVal;

            IsWow64Process(Process.GetCurrentProcess().Handle, out retVal);
            return retVal;
        }

        [DllImport(
            "kernel32.dll",
            SetLastError = true,
            CallingConvention = CallingConvention.Winapi
        )]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWow64Process([In] IntPtr process, [Out] out bool wow64Process);

        public static string GetMachineType()
        {
            switch (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE"))
            {
                case "x86":
                    return "Desktop";
                case "ARM64":
                    return "Tablet";
                default:
                    return "Laptop";
            }
        }

        // public static bool MatchFound(Process p, List<string> apps)
        // {
        //     foreach (string app in apps)
        //     {
        //         if (app.Equals(p.MainModule.ModuleName))
        //         {
        //             return true;
        //         }
        //     }
        //     return false;
        // }

        public static bool MatchFound(Process p, List<string> apps)
        {
            foreach (string app in apps)
            {
                if (app.Equals(p.MainWindowTitle))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool GetUserDetails()
        {
            int sessionCount = 0;
            IntPtr ppSessionInfo = IntPtr.Zero;

            if (
                WindowsAccess.WTSEnumerateSessions(
                    IntPtr.Zero,
                    0,
                    1,
                    ref ppSessionInfo,
                    ref sessionCount
                ) == 0
            )
            {
                Log.Information("Unable to get the session details");
                return false;
            }
            var sizeOfWTS_SESSION_INFO = Marshal.SizeOf(typeof(WTS_SESSION_INFO));

            for (int i = 0; i < sessionCount; i++)
            {
                var currentSession = (WTS_SESSION_INFO)
                    Marshal.PtrToStructure(
                        ppSessionInfo + (i * sizeOfWTS_SESSION_INFO),
                        typeof(WTS_SESSION_INFO)
                    );

                if (currentSession.State == (int)WTS_CONNECTSTATE_CLASS.WTSActive)
                {
                    IntPtr newbuffer = IntPtr.Zero;
                    uint bytesReturned;
                    //Console.WriteLine("Session ID:" + currentSession.SessionID);
                    if (
                        WTSQuerySessionInformation(
                            IntPtr.Zero,
                            currentSession.SessionID,
                            (int)WTS_INFO_CLASS.WTSUserName,
                            out newbuffer,
                            out bytesReturned
                        )
                    )
                    {
                        userName = Marshal.PtrToStringAnsi(newbuffer);
                        WTSFreeMemory(newbuffer);
                    }
                    else
                    {
                        uint error = GetLastError();
                        Log.Information($"ERROR in fetching Session Username: {error}");
                        return false;
                    }

                    IntPtr pbuffer = IntPtr.Zero;
                    if (
                        WTSQuerySessionInformation(
                            IntPtr.Zero,
                            currentSession.SessionID,
                            (int)WTS_INFO_CLASS.WTSSessionInfo,
                            out pbuffer,
                            out bytesReturned
                        )
                    )
                    {
                        var wtsInfo = (WTSINFOA)Marshal.PtrToStructure(pbuffer, typeof(WTSINFOA));
                        Log.Information("Logon time: " + wtsInfo.LogonTime.ToLocalTime());
                        WTSFreeMemory(pbuffer);
                        logonDateTime = wtsInfo.LogonTime;
                        domainName = wtsInfo.Domain.Trim();
                        machineName = Environment.MachineName;
                    }
                    else
                    {
                        uint error = GetLastError();
                        Log.Information("Failed to query session information: " + error);
                        return false;
                    }
                    machineType = GetMachineType();
                    osVersion = Environment.OSVersion.ToString();
                    osType = Is64BitOperatingSystem() ? "64-Bit" : "32-Bit";
                }
            }

            WTSFreeMemory(ppSessionInfo);
            return true;
        }

        public static bool GetLogonDetails()
        {
            int sessionCount = 0;
            IntPtr ppSessionInfo = IntPtr.Zero;

            if (WTSEnumerateSessions(IntPtr.Zero, 0, 1, ref ppSessionInfo, ref sessionCount) == 0)
            {
                return false;
            }
            var sizeOfWTS_SESSION_INFO = Marshal.SizeOf(typeof(WTS_SESSION_INFO));

            for (int i = 0; i < sessionCount; i++)
            {
                var currentSession = (WTS_SESSION_INFO)
                    Marshal.PtrToStructure(
                        ppSessionInfo + (i * sizeOfWTS_SESSION_INFO),
                        typeof(WTS_SESSION_INFO)
                    );

                if (currentSession.State == (int)WTS_CONNECTSTATE_CLASS.WTSActive)
                {
                    IntPtr pbuffer = IntPtr.Zero;
                    uint bytesReturned;
                    if (
                        WTSQuerySessionInformation(
                            IntPtr.Zero,
                            currentSession.SessionID,
                            (int)WTS_INFO_CLASS.WTSSessionInfo,
                            out pbuffer,
                            out bytesReturned
                        )
                    )
                    {
                        var wtsInfo = (WTSINFOA)Marshal.PtrToStructure(pbuffer, typeof(WTSINFOA));
                        WTSFreeMemory(pbuffer);
                        logonDateTime = wtsInfo.LogonTime;
                    }
                    else
                    {
                        uint error = GetLastError();
                        return false;
                    }
                }
            }
            WTSFreeMemory(ppSessionInfo);
            return true;
        }

        public static void logoutUser()
        {
            // Uncomment if required this feature.
            //ExitWindowsEx(EWX_LOGOFF, 0);
        }

        public static bool IsFirstMinuteOfDay()
        {
            DateTime now = DateTime.Now.ToUniversalTime();
            DateTime startOfDay = DateTime.Today.ToUniversalTime();
            DateTime endOfFirstMinute = startOfDay.AddMinutes(1);
            return now >= startOfDay && now < endOfFirstMinute;
        }

        public static bool IsFirstSecondOfDay()
        {
            DateTime now = DateTime.Now.ToUniversalTime();
            DateTime startOfDay = DateTime.Today.ToUniversalTime();
            DateTime endOfFirstSecond = startOfDay.AddSeconds(1);
            return now >= startOfDay && now < endOfFirstSecond;
        }

        public static double GetExecutionSeconds()
        {
            // Get the current time
            DateTime now = DateTime.Now;

            // Get the next 12:00 AM (midnight) by adding one day to the current date and setting the time to 00:00:00
            DateTime nextMidnight = now.Date.AddDays(1);

            // Calculate the difference between the next midnight and the current time
            TimeSpan timeUntilMidnight = nextMidnight - now;

            // Get the total seconds
            double secondsUntilMidnight = timeUntilMidnight.TotalSeconds;

            Log.Information($"Seconds until midnight: {Math.Ceiling(secondsUntilMidnight)}");
            return Math.Ceiling(secondsUntilMidnight);
        }

        public static HttpClientHandler GetHttpClientHandler()
        {
            var clientHandler = new HttpClientHandler();
            clientHandler.ServerCertificateCustomValidationCallback = (
                sender,
                cert,
                chain,
                sslPolicyErrors
            ) =>
            {
                return true; // Bypass SSL certificate validation
            };
            return clientHandler;
        }

        public static void CheckActivity(object sender, ElapsedEventArgs e)
        {
            var lastInputInfo = new NativeMethods.LASTINPUTINFO
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.LASTINPUTINFO))
            };

            NativeMethods.GetLastInputInfo(ref lastInputInfo);
            Log.Information(
                "Inside Check Activity - " + lastInputInfo.dwTime + " : " + _lastInputTime
            );

            if (lastInputInfo.dwTime != _lastInputTime)
            {
                Log.Information("CheckActivity before Callback");
                _lastInputTime = lastInputInfo.dwTime;
                ActivityDetected?.Invoke(sender, EventArgs.Empty);
            }
        }

        public static string GetLoggedInUserName(int sessionId)
        {
            IntPtr buffer;
            uint bytesReturned;

            if (
                WTSQuerySessionInformation(
                    IntPtr.Zero,
                    sessionId,
                    (int)WTS_INFO_CLASS.WTSUserName,
                    out buffer,
                    out bytesReturned
                )
            )
            {
                string userName = Marshal.PtrToStringAnsi(buffer);
                WTSFreeMemory(buffer);
                return userName;
            }
            return null;
        }

        public static string GetLoggedInUserDomain(int sessionId)
        {
            IntPtr buffer;
            uint bytesReturned;

            if (
                WTSQuerySessionInformation(
                    IntPtr.Zero,
                    sessionId,
                    (int)WTS_INFO_CLASS.WTSDomainName,
                    out buffer,
                    out bytesReturned
                )
            )
            {
                string domainName = Marshal.PtrToStringAnsi(buffer);
                WTSFreeMemory(buffer);
                return domainName;
            }
            return null;
        }

        public static uint _lastInputTime;
        public static event EventHandler ActivityDetected;

        public static class NativeMethods
        {
            [StructLayout(LayoutKind.Sequential)]
            public struct LASTINPUTINFO
            {
                public uint cbSize;
                public uint dwTime;
            }

            [DllImport("user32.dll")]
            public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
        }
    }
}
