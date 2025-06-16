using MonitorUserStandalone.Entity;

namespace MonitorUserStandalone.Services;

public class ApplicationUsageLocal
{
    public int Id { get; set; }
    public string RequestBody { get; set; }
    public DateTime Timestamp { get; set; }
}

public interface IApplicationUsageLocalService
{
    void StoreRequestBody(UserActivity requestBody);
    ApplicationUsageLocal GetRequestById(int id);
    IEnumerable<ApplicationUsageLocal> GetAllRequests();
    void DeleteRequest(int id);
    void DeleteAll();
}
