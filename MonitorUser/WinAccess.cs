
using Serilog;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using static MontiorUserStandlone.TerminalServicesAPI;

namespace MontiorUserStandlone
{
    public static class TerminalServicesAPI
    {
        const int WTS_CURRENT_SERVER_HANDLE = 0;
        public enum WTS_INFO_CLASS
        {
            WTSInitialProgram,
            WTSApplicationName,
            WTSWorkingDirectory,
            WTSOEMId,
            WTSSessionId,
            WTSUserName,
            WTSWinStationName,
            WTSDomainName,
            WTSConnectState,
            WTSClientBuildNumber,
            WTSClientName,
            WTSClientDirectory,
            WTSClientProductId,
            WTSClientHardwareId,
            WTSClientAddress,
            WTSClientDisplay,
            WTSClientProtocolType,
            WTSIdleTime,
            WTSLogonTime,
            WTSIncomingBytes,
            WTSOutgoingBytes,
            WTSIncomingFrames,
            WTSOutgoingFrames,
            WTSClientInfo,
            WTSSessionInfo,
            WTSSessionInfoEx,
            WTSConfigInfo,
            WTSValidationInfo,
            WTSSessionAddressV4,
            WTSIsRemoteSession
        }

        
        [StructLayout(LayoutKind.Sequential)]
        public struct Luid
        {
            public uint LowPart;
            public int HighPart;
        }

        public const int LONGLONG_SIZE = 8; 

        public enum WTS_CONNECTSTATE_CLASS
        {
            WTSActive,              // User is logged on to the WinStation.
            WTSConnected,           // WinStation is connected to the client.
            WTSConnectQuery,        // In the process of connecting to the client.
            WTSShadow,              // Shadowing another WinStation.
            WTSDisconnected,        // WinStation logged on without client.
            WTSIdle,                // Waiting for client to connect.
            WTSListen,              // WinStation is listening for a connection.
            WTSReset,               // WinStation is being reset.
            WTSDown,                // WinStation is down due to an error.
            WTSInit,                // WinStation is initializing.
        }


        [DllImport("wtsapi32.dll", SetLastError = true)]
        static extern int WTSEnumerateSessions(
        IntPtr hServer,
        int reserved,
        int version,
        ref IntPtr ppSessionInfo,
        ref int pCount);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        static extern bool WTSQuerySessionInformation(
        IntPtr hServer,
        int sessionId,
        WTS_INFO_CLASS wtsInfoClass,
        out IntPtr ppBuffer,
        out uint pBytesReturned);

        [StructLayout(LayoutKind.Sequential)]
        public struct WTS_SESSION_INFO
        {
            public int SessionID;
            [MarshalAs(UnmanagedType.LPStr)]
            public string pWinStationName;
            public int State;
        }

        [DllImport("wtsapi32.dll", SetLastError = true)]
        static extern bool WTSQuerySessionInformation(
            IntPtr hServer,
            int sessionId,
            int wtsInfoClass,
            out IntPtr ppBuffer,
            out uint pBytesReturned);

        [DllImport("wtsapi32.dll")]
        static extern void WTSFreeMemory(IntPtr pMemory);

        [StructLayout(LayoutKind.Sequential)]
        public struct WTS_CLIENT_ADDRESS
        {
            public uint AddressFamily;  // AF_INET, AF_INET6, etc.
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public byte[] Address;      // The client's IP address, for IPv4, only the first two bytes are used.
        }

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

        [StructLayout(LayoutKind.Sequential)]
        public struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        public static string GetLoggedInUserName(int sessionId)
        {
            IntPtr buffer;
            uint bytesReturned;

            if (WTSQuerySessionInformation(IntPtr.Zero, sessionId, (int)WTS_INFO_CLASS.WTSUserName, out buffer, out bytesReturned))
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

            if (WTSQuerySessionInformation(IntPtr.Zero, sessionId, (int)WTS_INFO_CLASS.WTSDomainName, out buffer, out bytesReturned))
            {
                string domainName = Marshal.PtrToStringAnsi(buffer);
                WTSFreeMemory(buffer);
                return domainName;
            }
            return null;
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
                get
                {
                    return Encoding.ASCII.GetString(WinStationNameRaw);
                }
            }
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = DOMAIN_LENGTH)]
            public byte[] DomainRaw;
            public string Domain
            {
                get
                {
                    return Encoding.ASCII.GetString(DomainRaw);
                }
            }
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = USERNAME_LENGTH + 1)]
            public byte[] UserNameRaw;
            public string UserName
            {
                get
                {
                    return Encoding.ASCII.GetString(UserNameRaw);
                }
            }
            public long ConnectTimeUTC;
            public DateTime ConnectTime
            {
                get
                {
                    return DateTime.FromFileTimeUtc(ConnectTimeUTC);
                }
            }
            public long DisconnectTimeUTC;
            public DateTime DisconnectTime
            {
                get
                {
                    return DateTime.FromFileTimeUtc(DisconnectTimeUTC);
                }
            }
            public long LastInputTimeUTC;
            public DateTime LastInputTime
            {
                get
                {
                    return DateTime.FromFileTimeUtc(LastInputTimeUTC);
                }
            }
            public long LogonTimeUTC;
            public DateTime LogonTime
            {
                get
                {
                    return DateTime.FromFileTimeUtc(LogonTimeUTC);
                }
            }
            public long CurrentTimeUTC;
            public DateTime CurrentTime
            {
                get
                {
                    return DateTime.FromFileTimeUtc(CurrentTimeUTC);
                }
            }
        }
        public static DateTime GetLoggedinTime(int sessionId)
        {
            uint bytesReturned;
            IntPtr pbuffer = IntPtr.Zero;
            if (WTSQuerySessionInformation(IntPtr.Zero, sessionId, WTS_INFO_CLASS.WTSSessionInfo, out pbuffer, out bytesReturned))
            {
                var wtsInfo = (WTSINFOA)Marshal.PtrToStructure(pbuffer, typeof(WTSINFOA));
                Log.Information("Logon time: " + wtsInfo.LogonTime.ToLocalTime());
                WTSFreeMemory(pbuffer);
                DateTime logonDateTime = wtsInfo.LogonTime;
                return logonDateTime;

            }
            else
            {
                int error = Marshal.GetLastWin32Error();
                Log.Information("Failed LOGON to query session information: " + error);
                return DateTime.MinValue;
            }
        }

        public static int[] GetActiveLocalSession()
        {
            IntPtr ppSessionInfo = IntPtr.Zero;
            int count = 0;

            if (WTSEnumerateSessions(IntPtr.Zero, 0, 1, ref ppSessionInfo, ref count) == 0)
            {
                Log.Information("Unable to fetch Session Details" );
                return null;
            }
            Log.Information("Total Sessions " + count);
            int[] sessions = new int[count];

            IntPtr currentSession = ppSessionInfo;
            for (int i = 0; i < count; i++)
            {
                WTS_SESSION_INFO sessionInfo = (WTS_SESSION_INFO)Marshal.PtrToStructure(currentSession, typeof(WTS_SESSION_INFO));
                currentSession += Marshal.SizeOf(typeof(WTS_SESSION_INFO));
                if (sessionInfo.State == (int)WTS_CONNECTSTATE_CLASS.WTSActive)
                {
                        Log.Information($"{i} - Returning the Location sesssion as " + sessionInfo.SessionID);
                        sessions[i] = sessionInfo.SessionID;
                }
            }
            WTSFreeMemory(ppSessionInfo);
            return sessions;  // Remote Session
        }

        public static int[] GetActiveRemoteSession(SimpleLogger logger)
        {
            IntPtr ppSessionInfo = IntPtr.Zero;
            int count = 0;

            if (WTSEnumerateSessions(IntPtr.Zero, 0, 1, ref ppSessionInfo, ref count) == 0)
            {
                logger.Log("Unable to fetch Session Details - Remote");
                return null;
            }
            int[] sessions = new int[count];
            IntPtr currentSession = ppSessionInfo;
            for (int i = 0; i < count; i++)
            {
                WTS_SESSION_INFO sessionInfo = (WTS_SESSION_INFO)Marshal.PtrToStructure(currentSession, typeof(WTS_SESSION_INFO));
                currentSession += Marshal.SizeOf(typeof(WTS_SESSION_INFO));
                if (sessionInfo.State == (int)WTS_CONNECTSTATE_CLASS.WTSActive)
                {
                    if (IsSessionRemote(sessionInfo.SessionID))
                    {
                        Log.Information("Returning the Remote sesssion as " + sessionInfo.SessionID);

                        sessions[i] = sessionInfo.SessionID;
                    }
                }
                /* DateTime? logonTime = GetLoggedinTime(sessionInfo.SessionID);
                 if (logonTime.HasValue)
                 {
                     Console.WriteLine($"Session ID: {sessionInfo.SessionID}, Logon Time: {logonTime.Value}");
                 }*/
            }
            WTSFreeMemory(ppSessionInfo);
            Log.Information("Returning falsse");
            return sessions;  // Local Session
        }

        public static bool IsSessionRemote(int sessionId)
        {
            IntPtr buffer;
            uint bytesReturned;

            if (WTSQuerySessionInformation(IntPtr.Zero, sessionId, WTS_INFO_CLASS.WTSClientAddress, out buffer, out bytesReturned))
            {
                try
                {
                    WTS_CLIENT_ADDRESS clientAddress = (WTS_CLIENT_ADDRESS)Marshal.PtrToStructure(buffer, typeof(WTS_CLIENT_ADDRESS));

                    // Check if the address is not empty and does not represent a local address
                    if (clientAddress.AddressFamily == 2 /* AF_INET for IPv4 */ &&
                        !BitConverter.ToString(clientAddress.Address, 2, 4).Equals("00-00-00-00")) // Assuming zeros for a local connection
                    {
                        Log.Information("Client Address " + clientAddress.Address);
                        return true; // Remote
                    }
                }
                finally
                {
                    WTSFreeMemory(buffer);
                }
            }
            Log.Information("Returning as false since this is not remote");
            return false;
        }
    }

}
