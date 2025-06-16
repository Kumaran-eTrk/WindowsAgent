using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Configuration;
using MonitorUserStandalone.Entity;
using MonitorUserStandalone.Model;
using MonitorUserStandalone.Services;
using Newtonsoft.Json;
using Serilog;

namespace MontiorUserStandlone
{
  public class ServiceHttpRequests
  {
    public static string _jwtToken { get; set; }

    public static bool _screenshot { get; set; }
    private string? _url { get; set; }
    private IApplicationUsageLocalService _usageLocalService;
    private string _dbPath { get; set; }

    static string localPath = Path.Combine(
      Directory.GetCurrentDirectory(),
      "..",
      "LatestVersion.zip"
    );

    public ServiceHttpRequests(string url, string dbPath)
    {
      this._url = url;
      this._dbPath = dbPath;
      var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
    }

    public async Task<string> GetLatestVersion()
    {
      try
      {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization =
          new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);
        var response = await httpClient.GetAsync(_url + "api/monitoruser/currentversion");
        if (response != null && response.IsSuccessStatusCode)
        {
          Log.Information("Successfully queried the latest version");
          var uModelString = await response.Content.ReadAsStringAsync();
          Log.Information("Entire body + " + uModelString);
          UpdaterModel uModel = System.Text.Json.JsonSerializer.Deserialize<UpdaterModel>(
            uModelString
          );
          Log.Information("Version from DB " + uModel.Path);
          return uModel.Path;
        }
        else
        {
          Log.Debug("Unable to get latest version");
          return null;
        }
      }
      catch (Exception ex)
      {
        Log.Debug("Unable to get latest version" + ex.StackTrace);
        return null;
      }
    }

    //Method to send the app version setails
    public async Task AppVersion(AppVersion version)
    {
      Log.Information("app version - Inside MetaData");
      try
      {
        var httpClient = new HttpClient(WindowsAccess.GetHttpClientHandler());

        var jsonString = JsonConvert.SerializeObject(version);
        var content = new StringContent(jsonString, Encoding.UTF8, "application/json");
        Log.Information("app version - Request Payload : " + jsonString);

        httpClient.DefaultRequestHeaders.Authorization =
          new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);
        var response = await httpClient.PostAsync(_url + "api/monitoruser/appversion", content);
        if (response != null && response.IsSuccessStatusCode)
        {
          Log.Information("App Version sent Successfully");
        }
        else
        {
          Log.Debug("Unable to Send App Version");
        }
      }
      catch (HttpRequestException ex)
      {
        Log.Error("Exception while sending app version" + ex);

        Log.Debug(ex.StackTrace);
      }
    }

    //Method to send Update version
    public async Task UpdateLatestVersion(string version, string localPath)
    {
      Log.Information("Send Latetst Version Before Sending");
      try
      {
        var httpClient = new HttpClient(WindowsAccess.GetHttpClientHandler());
        var machine = "Windows";
        var jsonString = JsonConvert.SerializeObject(version);
        var content = new StringContent(jsonString, Encoding.UTF8, "application/json");
        Log.Information("Send Latest Version - Request Payload : " + jsonString);

        httpClient.DefaultRequestHeaders.Authorization =
          new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);
        var response = await httpClient.GetAsync(
          _url + "api/monitoruser/currentversion/" + version + "/" + machine
        );

        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
          var contentType = response.Content.Headers.ContentType.MediaType;

          if (contentType == "application/zip")
          {
            var responseStream = await response.Content.ReadAsStreamAsync();
            if (responseStream != null)
            {
              using (
                var fileStream = new FileStream(
                  localPath,
                  FileMode.Create,
                  FileAccess.Write,
                  FileShare.None
                )
              )
              {
                await responseStream.CopyToAsync(fileStream);
              }

              Log.Information($"File saved to {localPath}");

              //  KillProcess(ProcessName);
              Environment.Exit(0);
            }
            else
            {
              Log.Information("The response stream is empty. No file was created.");
            }
          }
        }
        else
        {
          Log.Error(
            $"Failed to get the latest version from server. Status code: {response.StatusCode}"
          );
        }
      }
      catch (HttpRequestException ex)
      {
        Log.Error("Exception while sending Send Latest Version" + ex);

        Log.Debug(ex.StackTrace);
      }
    }

    // Token Authentication Method
    public async Task TokenAuthentication(string productkey, string role)
    {
      try
      {
        var httpClient = new HttpClient(WindowsAccess.GetHttpClientHandler());
        var data = new { productkey = productkey, role = role };
        var jsonString = JsonConvert.SerializeObject(data);
        var content = new StringContent(jsonString, Encoding.UTF8, "application/json");
        Log.Information("agent token - Request Payload : " + jsonString);

        HttpResponseMessage response = await httpClient.PostAsync(
          _url + "api/v1/monitoruser/agenttoken",
          content
        );

        if (response != null && response.IsSuccessStatusCode)
        {
          var responseString = await response.Content.ReadAsStringAsync();
          var accessTokenResponse = JsonConvert.DeserializeObject<AccessTokenResponse>(
            responseString
          );

          _jwtToken = accessTokenResponse.accesstoken;

          Log.Information("agent token sent Successfully");
        }
        else
        {
          Log.Debug("Unable to Send agent token");
        }
      }
      catch (HttpRequestException ex)
      {
        Log.Error("Exception while sending agent token" + ex);

        Log.Debug(ex.StackTrace);
      }
    }

    public async Task SendScreenshotConfiguration(ScreenshotConfiguration req)
    {
      try
      {
        var httpClient = new HttpClient(WindowsAccess.GetHttpClientHandler());

        var jsonString = JsonConvert.SerializeObject(req);
        var content = new StringContent(jsonString, Encoding.UTF8, "application/json");
        Log.Information("screenshot configuration - Request Payload : " + jsonString);

        HttpResponseMessage response = await httpClient.PostAsync(
          _url + "api/v1/monitoruser/screenshotconfig",
          content
        );

        if (response != null && response.IsSuccessStatusCode)
        {
          var responseString = await response.Content.ReadAsStringAsync();
          Log.Information("response" + responseString);
          var screenshotResponse = JsonConvert.DeserializeObject<ScreenshotConfigurationResponse>(
            responseString
          );
          Log.Information("screenshot response " + screenshotResponse.Result.Screenshot);
          _screenshot = screenshotResponse.Result.Screenshot;
          Log.Information("screenshot config " + _screenshot);
          Log.Information("screenshot config retrieved Successfully");
        }
        else
        {
          _screenshot = true;
          Log.Debug("Unable to Send screenshot config");
        }
      }
      catch (HttpRequestException ex)
      {
        Log.Error("Exception while sending screenshot" + ex);

        Log.Debug(ex.StackTrace);
      }
    }

    public static void KillProcess(string processName)
    {
      try
      {
        Process[] processes = Process.GetProcessesByName(processName);
        foreach (Process process in processes)
        {
          process.Kill();
          process.WaitForExit();
          Log.Information($"Process '{processName}' (ID: {process.Id}) has been killed.");
        }

        if (processes.Length == 0)
        {
          Log.Information($"No running process named '{processName}' was found.");
        }
      }
      catch (Exception ex)
      {
        Log.Information($"An error occurred while killing the process: {ex.Message}");
      }
    }

    //Method to send the user metadata setails
    public async Task SendUserMetaDetails(UserMetadata userData)
    {
      Log.Information("SendUserMetaDetails - Inside MetaData");
      try
      {
        var httpClient = new HttpClient(WindowsAccess.GetHttpClientHandler());

        var jsonString = JsonConvert.SerializeObject(userData);
        var content = new StringContent(jsonString, Encoding.UTF8, "application/json");
        Log.Information("SendUserMetaDetails - Request Payload : " + jsonString);

        //httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer "+_jwtToken);

        httpClient.DefaultRequestHeaders.Authorization =
          new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);
        var response = await httpClient.PostAsync(_url + "api/monitoruser/usermetadata", content);
        if (response != null && response.IsSuccessStatusCode)
        {
          Log.Information("User MetaData sent Successfully");
        }
        else
        {
          Log.Debug("Unable to Send User Meta Data");
        }
      }
      catch (HttpRequestException ex)
      {
        Log.Error("Exception while sending SendUserMetaDetails" + ex);
        Log.Debug(ex.StackTrace);
      }
    }

    //Method to send activity details
    public async Task SendActivityDetails(UserActivity userActivity)
    {
      try
      {
        Log.Information(
          "---------------" + userActivity.UserName + " : " + userActivity.DomainName
        );

        var httpClient = new HttpClient(WindowsAccess.GetHttpClientHandler());

        var jsonString = JsonConvert.SerializeObject(userActivity);
        var content = new StringContent(jsonString, Encoding.UTF8, "application/json");
        Log.Information(jsonString);

        long bodySize = Encoding.UTF8.GetByteCount(jsonString);

        // Measure ALL HTTP headers size
        long headerSize = 0;
        foreach (var header in httpClient.DefaultRequestHeaders)
        {
          string headerString = $"{header.Key}: {string.Join(",", header.Value)}\r\n";
          headerSize += Encoding.UTF8.GetByteCount(headerString);
        }

        // Get full HTTP request size (body + headers)
        using (var ms = new MemoryStream())
        {
          await content.CopyToAsync(ms);
          long fullBodySize = ms.Length; // Correct body size

          long totalRequestSize = headerSize + fullBodySize;

          Log.Information(
            $"Body Size: {fullBodySize} bytes | Headers: {headerSize} bytes | Total: {totalRequestSize / 1024.0} KB"
          );
        }

        httpClient.DefaultRequestHeaders.Authorization =
          new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);
        var response = await httpClient.PostAsync(_url + "api/monitoruser/useractivities", content);
        if (response != null && response.IsSuccessStatusCode)
        {
          Log.Information("User Activities Sent Successfully");
        }
        else
        {
          StoreActivityLocally(userActivity);
          LastInputInfoApp.SendToken(); // If 403 error exist's
          Log.Debug("Unable to Send the User Activities :( " + response.IsSuccessStatusCode);
        }
      }
      catch (Exception ex)
      {
        StoreActivityLocally(userActivity);
        Log.Error("Server error in SendActivityDetails:" + ex);

        Log.Debug(ex.StackTrace);
      }
    }

    private void StoreActivityLocally(UserActivity userActivity)
    {
      try
      {
        var dbHelper = new DatabaseHelper(_dbPath);
        _usageLocalService = new ApplicationUsageLocalService(dbHelper);
        _usageLocalService.StoreRequestBody(userActivity);
        Log.Information("****Data Backup Started****");
      }
      catch (Exception dbEx)
      {
        Log.Error($"Database error in StoreActivityLocally: {dbEx.Message}");
        Log.Debug(dbEx.StackTrace);
      }
    }

    //Method to send user logging details
    public async Task SendUserLoggingDetails(UserLoggingActivity userLoggingActivity)
    {
      try
      {
        var httpClient = new HttpClient(WindowsAccess.GetHttpClientHandler());

        var jsonString = JsonConvert.SerializeObject(userLoggingActivity);
        var content = new StringContent(jsonString, Encoding.UTF8, "application/json");
        Log.Information("SendUserLoggingDetails - " + jsonString);

        httpClient.DefaultRequestHeaders.Authorization =
          new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);
        var response = await httpClient.PostAsync(_url + "api/monitoruser/userlogging", content);
        if (response != null && response.IsSuccessStatusCode)
        {
          Log.Information("User Logging Details Sent Successfully");
        }
        else
        {
          Log.Debug("Unable to sent User Logging Details");
        }
      }
      catch (Exception ex)
      {
        Log.Error("Unable to sent User Logging Details" + ex);

        Log.Debug(ex.StackTrace);
      }
    }

    //Method to send IP Address
    public async Task SendIPAddress(IPAddressInfo iPAddressInfo)
    {
      try
      {
        var httpClient = new HttpClient(WindowsAccess.GetHttpClientHandler());
        var jsonString = JsonConvert.SerializeObject(iPAddressInfo);
        var content = new StringContent(jsonString, Encoding.UTF8, "application/json");
        Log.Information("SendIPAddress Request Payload :" + jsonString);

        httpClient.DefaultRequestHeaders.Authorization =
          new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);
        var response = await httpClient.PostAsync(_url + "api/monitoruser/ipaddressinfo", content);
        if (response != null && response.IsSuccessStatusCode)
        {
          Log.Information("IP Address Sent Successfully");
        }
        else
        {
          Log.Debug("Unable to send IP Address Details");
        }
      }
      catch (Exception ex)
      {
        Log.Error("Unable to send IP Address Details" + ex);
        Log.Debug(ex.StackTrace);
      }
    }

    public async Task SendExtensionDetails(ExtensionManifest extensiondata)
    {
      try
      {
        var httpClient = new HttpClient(WindowsAccess.GetHttpClientHandler());

        var jsonString = JsonConvert.SerializeObject(extensiondata);
        var content = new StringContent(jsonString, Encoding.UTF8, "application/json");
        Log.Information("SendIPAddress Request Payload :" + jsonString);

        httpClient.DefaultRequestHeaders.Authorization =
          new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);
        var response = await httpClient.PostAsync(
          _url + "api/monitoruser/extensiondetails",
          content
        );
        if (response != null && response.IsSuccessStatusCode)
        {
          Log.Information("IP Address Sent Successfully");
        }
        else
        {
          Log.Debug("Unable to send IP Address Details");
        }
      }
      catch (Exception ex)
      {
        Log.Error("Server error:" + ex);
      }
    }

    public async Task GetInstalledSoftware(Softwareinfo softwareinfo)
    {
      try
      {
        var httpClient = new HttpClient(WindowsAccess.GetHttpClientHandler());

        var jsonString = JsonConvert.SerializeObject(softwareinfo);
        var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

        long bodySize = Encoding.UTF8.GetByteCount(jsonString);

        // Measure ALL HTTP headers size
        long headerSize = 0;
        foreach (var header in httpClient.DefaultRequestHeaders)
        {
          string headerString = $"{header.Key}: {string.Join(",", header.Value)}\r\n";
          headerSize += Encoding.UTF8.GetByteCount(headerString);
        }

        // Get full HTTP request size (body + headers)
        using (var ms = new MemoryStream())
        {
          await content.CopyToAsync(ms);
          long fullBodySize = ms.Length; // Correct body size

          long totalRequestSize = headerSize + fullBodySize;

          Log.Information(
            $"Body Size: {fullBodySize} bytes | Headers: {headerSize} bytes | Total: {totalRequestSize / 1024.0} KB"
          );
        }

        httpClient.DefaultRequestHeaders.Authorization =
          new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);
        var response = await httpClient.PostAsync(
          _url + "api/monitoruser/softwaredetails",
          content
        );
        if (response != null && response.IsSuccessStatusCode)
        {
          Log.Information("Software Details Sent Successfully");
        }
        else
        {
          Log.Debug("Unable to Software Details");
        }
      }
      catch (Exception ex)
      {
        Log.Error("Server software error:" + ex.InnerException);
      }
    }
  }
}
