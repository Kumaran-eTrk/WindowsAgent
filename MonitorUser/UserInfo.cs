using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MonitorUserStandalone.Entity
{
    public class UserInfo
    {
        public string? username { get; set; }
        public string? domainname { get; set; }
        public DateTime logonTime { get; set; }
        public DateTime currentTime { get; set; }
    }

    public class IPAddressInfo
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserName { get; set; }
        public string IPAddress { get; set; }
        public string MacAddress { get; set; }
        public DateTime RecordDateTime { get; set; }
        public string? City { get; set; } // Optional, based on whether you want to store city
        public string? State { get; set; } // Optional
        public string? Country { get; set; } // Optional
    }

    public class ApplicationUsage
    {
        public string? Id { get; set; }
        public string? Application { get; set; }

        public string? ApplicationName { get; set; }

        public DateTime? StartDateTime { get; set; }
        public DateTime? EndDateTime { get; set; }
        public TimeSpan? Duration =>
            (TimeSpan)(EndDateTime.HasValue ? (EndDateTime.Value - StartDateTime) : TimeSpan.Zero);
        public byte[]? Screenshot { get; set; }
        public DateTime? RecordDateTime { get; set; }

        [ForeignKey("UserActivityUserName,UserActivityDomainName,UserActivityCurrentDateTime")]
        public string? UserActivityUserName { get; set; }
        public string? UserActivityDomainName { get; set; }
        public DateTime? UserActivityCurrentDateTime { get; set; }
        public virtual UserActivity? UserActivity { get; set; }
    }

    public class BrowserHistory
    {
        public string? Id { get; set; }
        public string? URL { get; set; }
        public string? Title { get; set; } // Title of the web page
        public DateTime? VisitTime { get; set; }
        public string? BrowserName { get; set; } // E.g., Chrome, Firefox, etc.

        [ForeignKey("UserActivityUserName,UserActivityDomainName,UserActivityCurrentDateTime")]
        public string? UserActivityUserName { get; set; }
        public string? UserActivityDomainName { get; set; }
        public DateTime? UserActivityCurrentDateTime { get; set; }
        public virtual UserActivity? UserActivity { get; set; }
    }

    public class AppVersion
    {
        public string version { get; set; }

        public string UserName { get; set; }

        public string DomainName { get; set; }
    }

    public enum AcceptanceStatus
    {
        unknown, // Default state, assuming no specific status is set
        accepted,
        rejected
    }

    public class ExtensionManifest
    {
        public string? Id { get; set; } = Guid.NewGuid().ToString();
        public string name { get; set; }
        public string description { get; set; }
        public string version { get; set; }

        public List<string> permissions { get; set; }

        public string browser { get; set; }

        public string username { get; set; }

        public DateTime modifieddatetime { get; set; }
        public AcceptanceStatus Status { get; set; }

        public ExtensionManifest()
        {
            modifieddatetime = DateTime.UtcNow;
            Status = AcceptanceStatus.unknown;
        }
    }

    public class AccessTokenResponse
    {
        public string accesstoken { get; set; }
    }

    public class Softwareinfo
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }

        public string Version { get; set; }
        public string UserName { get; set; }

        public DateTime ModifiedDateTime { get; set; }

        public string InstalledDate { get; set; }

        public AcceptanceStatus Status { get; set; }

        public Softwareinfo()
        {
            ModifiedDateTime = DateTime.UtcNow;
            Status = AcceptanceStatus.unknown;
        }
    }

    public class UserMetadata
    {
        [Key]
        public required string UserName { get; set; }
        public required string DomainName { get; set; }
        public required string MachineName { get; set; } // Name of the computer
        public required string OSVersion { get; set; } // OS version e.g. "Windows 10"
        public required string OSType { get; set; } // OS type e.g. "64-bit"
        public string? MachineType { get; set; } // e.g. "Desktop", "Laptop", "Tablet"
        public DateTime RecordDateTime { get; set; }
    }

    public class UserActivity
    {
        public string? UserName { get; set; }
        public string? DomainName { get; set; }
        public DateTime CurrentDateTime { get; set; }
        public bool? IsSessionLocked { get; set; }
        public long TotalIdleTime { get; set; }

        public virtual ICollection<ApplicationUsage>? ActiveApplications { get; set; } =
            new List<ApplicationUsage>();
        public virtual ICollection<BrowserHistory>? BrowserHistory { get; set; } =
            new List<BrowserHistory>();
    }

    public class ScreenshotConfiguration
    {
        public string UserName { get; set; }
        public string DomainName { get; set; }
    }

    public class ScreenshotConfigurationResponse
    {
        public Result Result { get; set; }
    }

    public class Result
    {
        public string Username { get; set; }
        public string Domainname { get; set; }
        public bool Screenshot { get; set; }
    }

    public class UserLoggingActivity
    {
        [Key]
        public string? UserName { get; set; }
        public string? DomainName { get; set; }
        public DateTime? CurrentDateTime { get; set; }
        public DateTime? LastLogonDateTime { get; set; }
        public DateTime? LastLogoutDateTime { get; set; }
    }
}
