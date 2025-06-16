using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MonitorUserStandalone.Entity;
using MontiorUserStandlone;
using Newtonsoft.Json;
using Serilog;

namespace MonitorUserStandalone.Services;

public class BackgroundSyncService
{
    private readonly IApplicationUsageLocalService _localService;
    private DatabaseHelper _databaseHelper;
    private readonly string _jwtToken;
    private readonly string _url;
    private HttpClient _httpClient;

    public BackgroundSyncService(string url, string dbpath, string token)
    {
        _databaseHelper = new DatabaseHelper(dbpath);
        _localService = new ApplicationUsageLocalService(_databaseHelper);
        _url = url;
        _jwtToken = token;
    }

    public async Task SyncLocalData()
    {
        try
        {
            List<ApplicationUsageLocal> localActivities = _localService.GetAllRequests().ToList();
            if (localActivities.Count == 0 || !IsServerAvailable(_url + "api/health"))
                return;
            Log.Information($"******************************************");
            Log.Information($"******      BACKUP RUNNING...      *******");
            Log.Information($"******************************************");
            Console.WriteLine($"******      BACKUP RUNNING...      *******");
            LastInputInfoApp.ScreenshotConfiguration();
            foreach (ApplicationUsageLocal activity in localActivities)
            {
                try
                {
                    var httpClient = new HttpClient(WindowsAccess.GetHttpClientHandler());
                    //UserActivity deserializedActivity = JsonConvert.DeserializeObject<UserActivity>(activity.RequestHash);
                    var jsonData = activity.RequestBody;
                    var content = new StringContent(
                        activity.RequestBody,
                        Encoding.UTF8,
                        "application/json"
                    );
                    string json = JsonConvert.SerializeObject(activity.RequestBody);
                    long bodySize = Encoding.UTF8.GetByteCount(json);

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
                        Console.WriteLine(
                            $"Body Size: {fullBodySize} bytes | Headers: {headerSize} bytes | Total: {totalRequestSize / 1024.0} KB"
                        );
                        Log.Information(
                            $"Body Size: {fullBodySize} bytes | Headers: {headerSize} bytes | Total: {totalRequestSize / 1024.0} KB"
                        );
                    }
                    //Log.Information(jsonString);


                    httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);
                    var response = await httpClient.PostAsync(
                        _url + "api/monitoruser/useractivities",
                        content
                    );
                    if (response.IsSuccessStatusCode)
                    {
                        _localService.DeleteRequest(activity.Id);
                        Log.Information($"Successfully synced activity ID {activity.Id}");
                        Console.WriteLine($"Successfully synced activity ID {activity.Id}");
                    }
                    else
                    {
                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            Log.Information("Due to Token issue ,backup process is terminated");
                            return;
                        }
                        Console.WriteLine(
                            $"Failed to sync activity ID {activity.Id}: {response.StatusCode}"
                        );
                        Log.Warning(
                            $"Failed to sync activity ID {activity.Id}: {response.StatusCode}"
                        );
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Error in syncing the actitity: {ex.Message}");
                    Log.Error($"Error in syncing the actitity: {ex.InnerException}");
                }
            }
            _databaseHelper.RefreshDB();
            Log.Information($"******************************************************");
            Log.Information($"******      BACKUP COMPLETED SUCCESSFULLY      *******");
            Log.Information($"******************************************************");
        }
        catch (Exception ex)
        {
            Log.Error($"Error during local data sync: {ex.Message}");
        }
    }

    private bool IsServerAvailable(string serverUrl)
    {
        try
        {
            using (var client = new HttpClient(WindowsAccess.GetHttpClientHandler()))
            {
                var response = client.GetAsync(serverUrl).Result;
                Log.Information("response form httpclient : ", response);
                return response.IsSuccessStatusCode;
            }
        }
        catch
        {
            return false;
        }
    }
}
