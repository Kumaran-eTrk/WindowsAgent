using System;
using System.CodeDom;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.IO.Pipes;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Timers;
using Microsoft.CSharp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using MonitorUserStandalone.Entity;
using MonitorUserStandalone.Services;
using MontiorUserStandlone;
using Newtonsoft.Json;
using Serilog;
using Serilog.Core;

public class LastInputInfoApp
{
  const int SW_HIDE = 0;
  const int SW_SHOWDEFAULT = 10;
  private static string? serverURL;
  public static string? ProductKey;
  int appDetailInterval = 60;
  int logonDetailsInterval = 300;
  static int startHours = 0;
  static int startMinutes = 0;
  static int endHours = 0;
  static int endMinutes = 0;
  static int interval_1 = 0;
  static int interval_2 = 0;
  static int interval_3 = 0;
  static int interval_4 = 0;
  static int interval_5 = 0;
  static int interval_6 = 0;
  static double interval_7 = 0;
  static string installationDirectory;
  static int userSessionID = 0;
  static string currentUserName = "";
  static string userDomainName = "";
  static string version = "";

  static string appVersion;
  static DateTime _lastTimeShortIntervalTaskRun = DateTime.Now;
  static DateTime currentDate = DateTime.Now;
  static string _dbpath = "";
  static string localPath = Path.Combine(
    Directory.GetCurrentDirectory(),
    "..",
    "LatestVersion.zip"
  );

  public static async Task Main(string[] args)
  {
    Assembly assembly = Assembly.GetExecutingAssembly();
    version = assembly.GetName().Version.ToString();

    var handle = WindowsAccess.GetConsoleWindow();
    WindowsAccess.ShowWindow(handle, SW_SHOWDEFAULT);
    WindowsAccess.ShowWindow(handle, SW_HIDE);

    Stopwatch stopwatch = new Stopwatch();
    stopwatch.Start();

    var configuration = new ConfigurationBuilder()
      .SetBasePath(System.IO.Directory.GetCurrentDirectory()) //set the base directory
      .AddJsonFile("appsettings.json")
      .Build();

    var logPathFormat = configuration.GetSection("Serilog:WriteTo:0:Args:pathFormat").Value;
    var dynamicPrefix = Environment.UserName + "-umlogs"; // This can be any value determined at runtime
    string userDirectory =
      Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + logPathFormat;

    Log.Logger = new LoggerConfiguration()
      .ReadFrom.Configuration(configuration)
      .WriteTo.File(
        Path.Combine(userDirectory, $"{dynamicPrefix}.txt"),
        rollingInterval: RollingInterval.Day
      )
      .CreateLogger();
    readAppSettings();

    int fiveSecondsIntervalCounter = 0;
    int tenSecondsIntervalCounter = 0;
    int minuteIntervalCounter = 0;
    Thread _monitorThread;
    System.Timers.Timer _timer;
    userSessionID = (int)WindowsAccess.GetMySessionId();
    Log.Information("My Session ID ----> " + userSessionID);

    try
    {
      WindowsAccess.ActivityDetected += (sender, e) =>
      {
        var currTime = DateTime.Now;
        uint idleTime = (uint)(currTime - _lastTimeShortIntervalTaskRun).TotalSeconds;
        Log.Information($"ExecuteAsync - Within Activity Detected {idleTime} - {interval_3}");
        _lastTimeShortIntervalTaskRun = currTime;

        if (idleTime > interval_3)
        {
          Log.Information("ExecuteAsync - Before SendAppDetails " + currTime);
          SendAppDetails(idleTime);
        }
      };
    }
    catch (Exception ex)
    {
      Log.Error("Server error:" + ex);
      Log.Debug(ex.StackTrace);
    }

    _monitorThread = new Thread(() =>
    {
      Log.Information("Within Thread");
      _timer = new System.Timers.Timer(1000); // Check every second
      _timer.Elapsed += WindowsAccess.CheckActivity;
      _timer.AutoReset = true;
      _timer.Start();
      Log.Information("After Timer");
    });

    int counter = 0;
    Log.Information("Before Send User Infor");
    _monitorThread.Start();

    try
    {
      ScreenshotConfiguration();
      SendToken();
      SendAppVersion();
      SendUserMetaData();
      SendIPMacAddress();
      SendLogonDetails();
    }
    catch (Exception ex)
    {
      Log.Error("Server error:" + ex);
      Log.Debug(ex.StackTrace);
    }

    interval_7 = WindowsAccess.GetExecutionSeconds();

    while (true)
    {
      try
      {
        Log.Information("Inside While");

        counter++;
        if (counter % interval_1 == 0)
        {
          // SendIPMacAddress();
          Log.Information("Interval 1");
        }
        if (counter % interval_2 == 0)
        {
          Log.Information("Interval 2");
        }
        if (counter % interval_3 == 0)
        {
          Log.Information("Interval 3");
        }
        if (counter % interval_4 == 0)
        {
          SendUserMetaData();
          SendIPMacAddress();
          SendLogonDetails();
          //Send the App Details when the user is continuously active with Idle Time as 0
          SendAppDetails(0);

          Log.Information("Interval 4");
        }

        if (counter % interval_5 == 0)
        {
          SendExtensionDetails();
          GetInstalledSoftware();
          Log.Information("Interval 5");
        }
        if (counter % interval_6 == 0)
        {
          var backgroundSyncService = new BackgroundSyncService(
            serverURL,
            _dbpath,
            ServiceHttpRequests._jwtToken
          );
          backgroundSyncService.SyncLocalData();
        }
        if (counter % interval_7 == 0)
        {
          Log.Information("execution time started for token and version");
          SendToken();
          ScreenshotConfiguration();
          SendAppVersion();
          interval_7 += 86400;
        }

        if (WindowsAccess.IsFirstMinuteOfDay())
        {
          Log.Information("First Minute of the day");
          SendUserMetaData();
          SendIPMacAddress();
          SendLogonDetails();

          WindowsAccess.logoutUser();
        }

        if (DateTime.Now.Date != currentDate.Date)
        {
          Log.Information(
            "ExecuteAsync - Calling CheckAndUpdateVersion "
              + DateTime.Now
              + " : "
              + currentDate.Date
          );
          currentDate = DateTime.Now;
          WindowsAccess.totalIdleTime = 0;
          WindowsAccess.idleTicks = 0;
          // Whenever the service comes to live and it sees a new date, it should logout the user. :)
          WindowsAccess.logoutUser();

          ScreenshotConfiguration();
          SendUserMetaData();
          SendIPMacAddress();
          SendLogonDetails();
          SendToken();
        }

        await Task.Delay(1000);
        // Delay for 1 second
      }
      catch (Exception ex)
      {
        Log.Error("Server error inside while:" + ex);
        Log.Debug(ex.StackTrace);
      }
    }
  }

  //Method to read the appsettings.json
  static void readAppSettings()
  {
    try
    {
      Log.Information("Within ReadAppSettings");
      var builder = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

      IConfigurationRoot configuration = builder.Build();

      serverURL = configuration["api:url"];
      interval_1 = int.Parse(configuration["interval_1"]);
      interval_2 = int.Parse(configuration["interval_2"]);
      interval_3 = int.Parse(configuration["interval_3"]);
      interval_4 = int.Parse(configuration["interval_4"]);
      interval_5 = int.Parse(configuration["interval_5"]);
      interval_6 = int.Parse(configuration["interval_6"]);

      ProductKey = configuration["ProductKey"];
      _dbpath = configuration["sqlite"];
      Log.Information(
        "Within ReadAppSettings : "
          + interval_1
          + " : "
          + interval_2
          + " : "
          + interval_3
          + " : "
          + interval_4
          + " : "
          + interval_5
      );
      parseWorkingHours(configuration["workinghours"]);

      installationDirectory = Directory.GetCurrentDirectory();
      if (installationDirectory == null || installationDirectory.Equals(""))
      {
        installationDirectory = configuration["installationDirectory"];
      }
    }
    catch (Exception ex)
    {
      Log.Error("Server error in reading appsettings:" + ex);
      Log.Debug(ex.StackTrace);
    }
  }

  //Method to parse working hours
  static void parseWorkingHours(string workinghours)
  {
    try
    {
      string[] parts = workinghours.Split('-');
      if (parts.Length != 2)
      {
        Log.Information(
          "ParseWorkingHours - Working Hours in appsettings.json is in invalid format"
        );
        return;
      }

      string[] startTimeParts = parts[0].Split(':');
      string[] endTimeParts = parts[1].Split(':');

      if (startTimeParts.Length != 2 || endTimeParts.Length != 2)
      {
        Log.Information("ParseWorkingHours - Invalid time format");
        return;
      }

      startHours = int.Parse(startTimeParts[0]);
      startMinutes = int.Parse(startTimeParts[1]);
      endHours = int.Parse(endTimeParts[0]);
      endMinutes = int.Parse(endTimeParts[1]);
      Log.Information(
        $"ParseWorkingHours - Start Hours: {startHours}, Start Minutes: {startMinutes}, End Hours: {endHours}, End Minutes: {endMinutes}"
      );
    }
    catch (Exception ex)
    {
      Log.Error("Server error in ParseWorkingHours:" + ex);
      Log.Debug(ex.StackTrace);
    }
  }

  //Method to send logon details
  protected static async void SendLogonDetails()
  {
    try
    {
      WindowsAccess.GetLogonDetails();
      UserLoggingActivity userLoggingActivity = new UserLoggingActivity
      {
        UserName = TerminalServicesAPI.GetLoggedInUserName(userSessionID).Trim(),
        DomainName = TerminalServicesAPI
          .GetLoggedInUserDomain(userSessionID)
          .Replace("\u0000", string.Empty),
        CurrentDateTime = DateTime.Now.ToUniversalTime(),
        LastLogonDateTime = WindowsAccess.logonDateTime.ToUniversalTime(),
      };
      Log.Information("SendLogonDetails - Before SendLogonDetails HTTP Requests");
      ServiceHttpRequests serviceHttpRequests = new ServiceHttpRequests(serverURL, _dbpath);
      await serviceHttpRequests.SendUserLoggingDetails(userLoggingActivity);
      Log.Information("SendLogonDetails - After SendLogonDetails HTTP Requests");
    }
    catch (Exception ex)
    {
      Log.Error("Server error:" + ex);
      Log.Debug(ex.StackTrace);
    }
  }

  public static async void SendToken()
  {
    try
    {
      string role = "Agent";

      Log.Information("SendToken - Before SendToken HTTP Requests");
      ServiceHttpRequests serviceHttpRequests = new ServiceHttpRequests(serverURL, _dbpath);
      await serviceHttpRequests.TokenAuthentication(ProductKey, role);
      Log.Information("SendToken - After SendToken HTTP Requests");
    }
    catch (Exception ex)
    {
      Log.Error("Server error:" + ex);
      Log.Debug(ex.StackTrace);
    }
  }

  public static async void ScreenshotConfiguration()
  {
    try
    {
      ScreenshotConfiguration request = new ScreenshotConfiguration
      {
        UserName = TerminalServicesAPI.GetLoggedInUserName(userSessionID).Trim(),
        DomainName = TerminalServicesAPI
          .GetLoggedInUserDomain(userSessionID)
          .Replace("\u0000", string.Empty),
      };

      Log.Information("Send Screenshot Configuration - Before Sending HTTP Requests");
      ServiceHttpRequests serviceHttpRequests = new ServiceHttpRequests(serverURL, _dbpath);
      await serviceHttpRequests.SendScreenshotConfiguration(request);
    }
    catch (Exception ex)
    {
      Log.Error("Sending screenshot config :" + ex);
      Log.Debug(ex.StackTrace);
    }
  }

  protected static async void SendAppVersion()
  {
    try
    {
      AppVersion App = new AppVersion
      {
        version = version,
        UserName = TerminalServicesAPI.GetLoggedInUserName(userSessionID).Trim(),
        DomainName = TerminalServicesAPI
          .GetLoggedInUserDomain(userSessionID)
          .Replace("\u0000", string.Empty),
      };
      Log.Information("App Version - Before App Version HTTP Requests");

      ServiceHttpRequests serviceHttpRequests = new ServiceHttpRequests(serverURL, _dbpath);
      await serviceHttpRequests.AppVersion(App);
      // await serviceHttpRequests.UpdateLatestVersion(App.version, localPath);

      Log.Information("App Version - After App Version HTTP Requests" + App.version);
    }
    catch (Exception ex)
    {
      Log.Error("Sending app version :" + ex);
      Log.Debug(ex.StackTrace);
    }
  }

  //Method to send IPAddresses
  protected static async void SendIPMacAddress()
  {
    try
    {
      string remoteIpAddress = "kumaran";
      int remotePort = 443;
      string localIpAddress = string.Empty;
      string macAddress = string.Empty;

      using (UdpClient udpClient = new UdpClient())
      {
        udpClient.Connect(remoteIpAddress, remotePort);
        IPEndPoint localEndPoint = udpClient.Client.LocalEndPoint as IPEndPoint;

        if (localEndPoint != null)
        {
          Log.Information($"Local IP Address: {localEndPoint.Address}");
          localIpAddress = localEndPoint.Address.ToString();

          // Get the corresponding MAC address
          macAddress = GetMacByIPAddress(localEndPoint.Address.ToString());
          if (!string.IsNullOrEmpty(macAddress))
          {
            Log.Information($"MAC Address: {macAddress}");
          }
        }
      }
      IPAddressInfo ipAddressMeta = new IPAddressInfo
      {
        UserName = TerminalServicesAPI.GetLoggedInUserName(userSessionID),
        IPAddress = localIpAddress,
        MacAddress = macAddress,
        RecordDateTime = DateTime.UtcNow,
      };
      Log.Information("SendIPMacAddress - Inside");
      ServiceHttpRequests sRequest = new ServiceHttpRequests(serverURL, _dbpath);
      await sRequest.SendIPAddress(ipAddressMeta);
    }
    catch (Exception ex)
    {
      Log.Error("Server error:" + ex);
      Log.Debug(ex.StackTrace);
    }
  }

  //Method to get the MAC by IPAddress
  private static string GetMacByIPAddress(string ipAddress)
  {
    try
    {
      foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
      {
        var ipProps = nic.GetIPProperties();
        if (ipProps.UnicastAddresses.Any(ua => ua.Address.ToString() == ipAddress))
        {
          return BitConverter.ToString(nic.GetPhysicalAddress().GetAddressBytes());
        }
      }
      return string.Empty;
    }
    catch (Exception ex)
    {
      Log.Error("unable to get the MAC by IP Address" + ex);
      Log.Debug(ex.StackTrace);
      return string.Empty;
    }
  }

  //Method to send idle time
  private static Boolean SendIdleTime()
  {
    Log.Information("Inside SendIdleTime");
    try
    {
      using (
        NamedPipeClientStream pipeClient = new NamedPipeClientStream(
          ".",
          "Global\\LastInputInfoPipe",
          PipeDirection.Out
        )
      )
      {
        pipeClient.Connect();

        using (StreamWriter writer = new StreamWriter(pipeClient))
        {
          TerminalServicesAPI.LASTINPUTINFO info = new TerminalServicesAPI.LASTINPUTINFO();
          info.cbSize = (uint)Marshal.SizeOf(info);

          if (TerminalServicesAPI.GetLastInputInfo(ref info))
          {
            uint idleTime = ((uint)Environment.TickCount - info.dwTime) / 60;
            Log.Information($"Last activities seconds {idleTime}");
            writer.WriteLine(idleTime);
          }
          else
          {
            //writer.WriteLine("Error in sending the GetLastInputInfo");
            Log.Information($"Last activities seconds FAILED");
            return false;
          }
        }
      }
      return true;
    }
    catch (Exception ex)
    {
      Log.Error("Server couldn't able to send the idle time");
      Log.Debug(ex.StackTrace);
      return false;
    }
  }

  //Method to send user information
  public static Boolean SendUserInfo()
  {
    Log.Information("Inside Send User Infor");
    try
    {
      using (
        NamedPipeClientStream pipeClient = new NamedPipeClientStream(
          ".",
          "Global\\UserMetaInformation",
          PipeDirection.Out
        )
      )
      {
        pipeClient.Connect();

        using (StreamWriter writer = new StreamWriter(pipeClient))
        {
          int[] currentSession = null;
          Log.Information("Before Fecthing Terminal Services API");
          try
          {
            currentSession = TerminalServicesAPI.GetActiveLocalSession();
            Log.Information(
              ".........................Current Local Sessions " + currentSession.Length
            );
          }
          catch (Exception ex)
          {
            Log.Information(ex.ToString());
            Log.Debug(ex.StackTrace);
          }
          foreach (int session in currentSession)
          {
            Log.Information("See the session ID:" + session);
            UserInfo info = new UserInfo();
            info.username = TerminalServicesAPI.GetLoggedInUserName(session);
            if (info.username == null || info.username.Trim().Length == 0)
            {
              continue;
            }
            Log.Information("Username: " + info.username);
            info.domainname = TerminalServicesAPI.GetLoggedInUserDomain(session);
            Log.Information("Domain: " + info.domainname);
            info.logonTime = TerminalServicesAPI.GetLoggedinTime(session);
            info.currentTime = DateTime.Now.ToUniversalTime();
            string jsonData = System.Text.Json.JsonSerializer.Serialize(info);
            Log.Information("Clearly" + jsonData);
            writer.Write(jsonData);
            writer.Flush();
          }
        }
      }
      return true;
    }
    catch (Exception ex)
    {
      Log.Error("Server couldn't able to send the user information");
      Log.Debug(ex.StackTrace);
      return false;
    }
  }

  public static async Task SendExtensionDetails()
  {
    try
    {
      // Perform any initialization or setup needed before processing extensions

      await ListExtensions("Chrome");
      await ListExtensions("Edge");

      // Additional logic can be added here for handling exceptions or sending details asynchronously
      Log.Information($"Processing complete extention");
    }
    catch (Exception ex)
    {
      Log.Information($"Error in processing extensions: {ex.Message}");
      // Add logic here to send exception details asynchronously, if needed
    }
  }

  static async Task ListExtensions(string browser)
  {
    string username = Environment.UserName;
    string extensionFolderPath = GetExtensionFolderPath(browser, username);

    await ListExtensionsInFolder(browser, extensionFolderPath);
  }

  static async Task ListExtensionsInFolder(string browser, string extensionFolderPath)
  {
    if (extensionFolderPath != null && Directory.Exists(extensionFolderPath))
    {
      string[] extensionFolders = Directory.GetDirectories(extensionFolderPath);

      foreach (string extensionFolder in extensionFolders)
      {
        string manifestPath = Path.Combine(extensionFolder, "manifest.json");

        // Check if manifest.json exists directly in the folder
        if (File.Exists(manifestPath))
        {
          try
          {
            string manifestContent = await File.ReadAllTextAsync(manifestPath);
            ExtensionManifest manifest =
              Newtonsoft.Json.JsonConvert.DeserializeObject<ExtensionManifest>(manifestContent);

            // Check if messages.json file exists
            string messagesPath = Path.Combine(extensionFolder, "_locales", "en", "messages.json");
            string extensionName = manifest.name;

            if (File.Exists(messagesPath))
            {
              try
              {
                string messagesContent = await File.ReadAllTextAsync(messagesPath);
                dynamic messagesJson = Newtonsoft.Json.JsonConvert.DeserializeObject(
                  messagesContent
                );

                // Extract extension name from messages.json
                if (messagesJson?.extName?.message != null)
                {
                  extensionName = messagesJson.extName.message;
                }
              }
              catch (Exception ex)
              {
                HandleManifestParsingError(browser, extensionFolder, ex);
              }
            }

            Log.Information(
              $"{browser} Extension Name: {extensionName}, Description: {manifest.description}, Version: {manifest.version}"
            );
          }
          catch (Exception ex)
          {
            HandleManifestParsingError(browser, extensionFolder, ex);
          }
        }
        else
        {
          // If manifest.json doesn't exist in the main folder, look for it in subfolders
          string[] subfolders = Directory.GetDirectories(extensionFolder);

          foreach (string subfolder in subfolders)
          {
            string subfolderManifestPath = Path.Combine(subfolder, "manifest.json");

            if (File.Exists(subfolderManifestPath))
            {
              try
              {
                string manifestContent = await File.ReadAllTextAsync(subfolderManifestPath);
                ExtensionManifest manifest =
                  Newtonsoft.Json.JsonConvert.DeserializeObject<ExtensionManifest>(manifestContent);

                // Check if messages.json file exists
                string messagesPath = Path.Combine(subfolder, "_locales", "en", "messages.json");
                string extensionName = manifest.name;
                List<string> permissions = new List<string>(); // New line for permissions

                if (File.Exists(messagesPath))
                {
                  try
                  {
                    string messagesContent = await File.ReadAllTextAsync(messagesPath);
                    dynamic messagesJson = Newtonsoft.Json.JsonConvert.DeserializeObject(
                      messagesContent
                    );

                    // Extract extension name from messages.json
                    if (messagesJson?.app_name?.message != null)
                    {
                      extensionName = messagesJson.app_name.message;
                    }
                    else if (messagesJson?.extName?.message != null)
                    {
                      extensionName = messagesJson.extName.message;
                    }
                  }
                  catch (Exception ex)
                  {
                    HandleManifestParsingError(browser, subfolder, ex);
                  }
                }

                // Extract permissions from manifest
                if (manifest.permissions != null)
                {
                  permissions.AddRange(manifest.permissions);
                }

                ExtensionManifest extensiondata = new ExtensionManifest
                {
                  name = extensionName,
                  description = manifest.description,
                  version = manifest.version,
                  permissions = permissions, // Assign permissions to the extensiondata
                  username = Environment.UserName,
                  browser = browser,
                  Status = AcceptanceStatus.unknown,
                };

                ServiceHttpRequests sRequest = new ServiceHttpRequests(serverURL, _dbpath);
                await sRequest.SendExtensionDetails(extensiondata);
                Log.Information("Json data" + extensiondata);
                Log.Information(
                  $"{browser} Extension Name: {extensionName}, Description: {manifest.description}, Version: {manifest.version}, Permissions: {string.Join(", ", permissions)},Username:{Environment.UserName},browser:{browser}"
                );
              }
              catch (Exception ex)
              {
                HandleManifestParsingError(browser, subfolder, ex);
              }
            }
          }
        }
      }
    }
    else
    {
      Log.Information($"{browser} extension folder not found for user {Environment.UserName}.");
    }
  }

  static void HandleManifestParsingError(string browser, string extensionFolder, Exception ex)
  {
    Log.Information(
      $"Error parsing manifest file for {browser} extension in folder {extensionFolder}: {ex.Message}"
    );
    // Add additional error handling or logging logic here if needed
  }

  static string GetExtensionFolderPath(string browser, string username)
  {
    string localAppDataPath = Environment.GetFolderPath(
      Environment.SpecialFolder.LocalApplicationData
    );

    switch (browser)
    {
      case "Chrome":
        return Path.Combine(
          localAppDataPath,
          "Google",
          "Chrome",
          "User Data",
          "Default",
          "Extensions"
        );

      case "Edge":
        return Path.Combine(
          localAppDataPath,
          "Microsoft",
          "Edge",
          "User Data",
          "Default",
          "Extensions"
        );
      default:
        return null;
    }
  }

  // Softwares Extraction

  public static void GetInstalledSoftware()
  {
    Log.Information("Software Entry Started");

    List<Softwareinfo> softwareList = new List<Softwareinfo>();

    // Query registry keys
    string[] registryUninstallKeys =
    {
      @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
      @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    };

    foreach (string uninstallKeyPath in registryUninstallKeys)
    {
      using (RegistryKey uninstallKey = Registry.LocalMachine.OpenSubKey(uninstallKeyPath))
      {
        if (uninstallKey != null)
        {
          GetInstalledSoftwareFromRegistryAsync(uninstallKey, softwareList);
        }
      }
    }

    // Query WMI for additional software information
    QueryWMISoftwareAsync(softwareList);
  }

  static async Task GetInstalledSoftwareFromRegistryAsync(
    RegistryKey uninstallKey,
    List<Softwareinfo> softwareList
  )
  {
    foreach (string subKeyName in uninstallKey.GetSubKeyNames())
    {
      try
      {
        using (RegistryKey subKey = uninstallKey.OpenSubKey(subKeyName))
        {
          object displayName = subKey.GetValue("DisplayName");
          object displayVersion = subKey.GetValue("DisplayVersion");
          object installDate = subKey.GetValue("InstallDate");

          if (displayName != null)
          {
            Softwareinfo softwareinfo = new Softwareinfo
            {
              Name = displayName.ToString(),
              Version = displayVersion?.ToString() ?? "N/A",
              InstalledDate = installDate?.ToString() ?? "N/A",
              UserName = Environment.UserName,
              ModifiedDateTime = DateTime.UtcNow,
              Status = AcceptanceStatus.unknown
            };
          }
        }
      }
      catch (Exception ex)
      {
        Log.Information($"Error accessing registry key: {ex.Message}");
      }
    }
  }

  static async Task QueryWMISoftwareAsync(List<Softwareinfo> softwareList)
  {
    try
    {
      // Query WMI for installed software
      ManagementObjectSearcher searcher = new ManagementObjectSearcher(
        "SELECT * FROM Win32_Product"
      );

      foreach (ManagementObject obj in searcher.Get())
      {
        string installDateStr = obj["InstallDate"] as string;
        if (!string.IsNullOrEmpty(installDateStr) && installDateStr.Length >= 8)
        {
          DateTime installDate;
          if (
            DateTime.TryParseExact(
              installDateStr.Substring(0, 8),
              "yyyyMMdd",
              CultureInfo.InvariantCulture,
              DateTimeStyles.None,
              out installDate
            )
          )
          {
            Softwareinfo softwareinfo = new Softwareinfo
            {
              Name = obj["Name"].ToString(),
              Version = obj["Version"].ToString(),
              InstalledDate = installDate.ToString("yyyy/MM/dd"),
              UserName = Environment.UserName,
              ModifiedDateTime = DateTime.UtcNow,
              Status = AcceptanceStatus.unknown
            };
            // softwareList.Add(softwareinfo);  ServiceHttpRequests sRequest = new ServiceHttpRequests(serverURL,_dbpath);
            ServiceHttpRequests sRequest = new ServiceHttpRequests(serverURL, _dbpath);
            await sRequest.GetInstalledSoftware(softwareinfo);
            Log.Information(
              "SendData - Inside" + System.Text.Json.JsonSerializer.Serialize(softwareinfo)
            );
          }
        }
      }
    }
    catch (Exception ex)
    {
      Log.Information($"Error querying WMI: {ex.Message}");
    }
  }

  //Method to send user metadata details
  protected static async void SendUserMetaData()
  {
    try
    {
      UserInfo info = new UserInfo();
      info.username = TerminalServicesAPI.GetLoggedInUserName(userSessionID);
      Log.Information(
        "SendUserMetaData - See the session ID:" + userSessionID + " : " + info.username
      );

      if (WindowsAccess.GetUserDetails())
      {
        UserMetadata userMeta = new UserMetadata
        {
          UserName = TerminalServicesAPI.GetLoggedInUserName(userSessionID),
          DomainName = TerminalServicesAPI
            .GetLoggedInUserDomain(userSessionID)
            .Replace("\u0000", string.Empty),
          MachineName = WindowsAccess.machineName,
          OSVersion = WindowsAccess.osVersion,
          OSType = WindowsAccess.osType,
          MachineType = WindowsAccess.machineType,
          RecordDateTime = DateTime.UtcNow
        };
        currentUserName = TerminalServicesAPI.GetLoggedInUserName(userSessionID);
        userDomainName = TerminalServicesAPI
          .GetLoggedInUserDomain(userSessionID)
          .Replace("\u0000", string.Empty);
        Log.Information(
          "SendUserMetaData - Inside" + System.Text.Json.JsonSerializer.Serialize(userMeta)
        );
        ServiceHttpRequests sRequest = new ServiceHttpRequests(serverURL, _dbpath);
        await sRequest.SendUserMetaDetails(userMeta);
      }
    }
    catch (Exception ex)
    {
      Log.Error("Server couldn't able to send the user metadata details");
      Log.Debug(ex.StackTrace);
    }
  }

  //Method to send application details
  static async void SendAppDetails(uint idleTime)
  {
    try
    {
      var aDate = DateTime.UtcNow.Date;
      Log.Information("SendAppDetails - Before SendAppDetails aDate Value ----> : " + aDate);

      UserActivity uActivity = new UserActivity
      {
        UserName = TerminalServicesAPI.GetLoggedInUserName(userSessionID),
        DomainName = TerminalServicesAPI
          .GetLoggedInUserDomain(userSessionID)
          .Replace("\u0000", string.Empty),
        //CurrentDateTime = new DateTime(today.Year, today.Month, today.Day, today.Hour, today.Minute, 0),
        CurrentDateTime = aDate,
        TotalIdleTime = idleTime,
        ActiveApplications = GetApplications(currentUserName, userDomainName, aDate),
        BrowserHistory = null,
      };
      Log.Information("SendAppDetails - Before SendAppDetails HTTP Requests");
      ServiceHttpRequests serviceHttpRequests = new ServiceHttpRequests(serverURL, _dbpath);
      await serviceHttpRequests.SendActivityDetails(uActivity);
      Log.Information("SendAppDetails - After SendActivity Details");
    }
    catch (Exception ex)
    {
      Log.Error("Server couldn't able to send the user metadata details");
      Log.Debug(ex.StackTrace);
    }
  }

  //Method to get the list of application used
  static List<ApplicationUsage> GetApplications(string UserName, string DomainName, DateTime today)
  {
    List<ApplicationUsage> apps = new List<ApplicationUsage>();
    try
    {
      Log.Information("GetApplications - Inside " + UserName + " : " + DomainName + " : " + today);

      Process[] localAll = Process.GetProcesses();

      List<string> appList = OpenedApplicationsFetcher.GetOpenedWindowsTitles();
      // List<string> appList = OpenedApplicationsFetcher.GetOpenedWindowsModuleNames();
      Log.Information("Application List size ---> " + appList.Count);
      string currentWindow = OpenedApplicationsFetcher.GetFocusedWindowTitle();
      int windowsCount = 0;

      var recordDateTime = DateTime.Now.ToUniversalTime();

      foreach (Process p in localAll)
      {
        if (p.MainWindowHandle != IntPtr.Zero && !p.HasExited)
        {
          //Log.Information("GetApplications - Inside Process Iteration " + p.MainWindowTitle + " : " + p.ProcessName + " : " + p.SessionId + " : " + userSessionID);
          windowsCount++;
          // if (WindowsAccess.MatchFound(p, appList))
          {
            Log.Information(
              "GetApplications - Inside Process Iteration "
                + p.MainWindowTitle
                + " : "
                + p.ProcessName
                + " : "
                + p.SessionId
                + " : "
                + userSessionID
            );
            if (p.SessionId == userSessionID)
            {
              string applicationName = GetApplicationName(p);

              string applicationTitle = p.MainWindowTitle.Trim();

              ApplicationUsage aUsage = new ApplicationUsage
              {
                Id = Guid.NewGuid().ToString(),
                Application = applicationTitle,
                ApplicationName = applicationName,
                StartDateTime = p.StartTime,
                EndDateTime = p.HasExited ? p.ExitTime : DateTime.MinValue,
                Screenshot = Screenshot.CaptureActiveWindow(p, ServiceHttpRequests._screenshot),
                UserActivityUserName = UserName,
                UserActivityDomainName = DomainName.Replace("\u0000", string.Empty).ToUpper(),
                UserActivityCurrentDateTime = today,
                RecordDateTime = DateTime.Now
              };

              apps.Add(aUsage);
              Log.Information(
                "Application name : "
                  + aUsage.ApplicationName
                  + " : "
                  + aUsage.RecordDateTime
                  + " "
                  + "Application : "
                  + aUsage.Application
              );
            }
          }
        }
      }

      return apps;
    }
    catch (Exception ex)
    {
      Log.Information("Couldn't able to send the application usage details " + ex.Message);
      Log.Debug(ex.StackTrace);
      return apps;
    }
  }

  static string GetApplicationName(Process process)
  {
    try
    {
      string moduleName = process.MainModule.ModuleName;
      string applicationName = Path.GetFileNameWithoutExtension(moduleName)?.ToLowerInvariant();
      return applicationName;
    }
    catch (Exception ex)
    {
      // Log the exception or handle it accordingly
      Log.Information(
        "Error getting application name: " + "  " + process.ProcessName + " " + ex.Message
      );
      return process.MainWindowTitle.Trim(); // Return window title if file description is not available
    }
  }
}
