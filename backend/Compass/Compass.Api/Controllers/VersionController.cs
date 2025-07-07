using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace Compass.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VersionController : ControllerBase
{
    [HttpGet]
    public IActionResult GetVersionInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
        var company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;

        return Ok(new
        {
            Product = product ?? "Governance Guardian",
            Version = informationalVersion ?? version?.ToString() ?? "Unknown",
            ApiVersion = "2.1.0",
            FrontendVersion = "1.0.0", // This should be read from a config or shared file
            BuildDate = GetBuildDate(),
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
            Company = company ?? "Silverfern Technology Consultants",
            Features = new[]
            {
                "Multi-Tenant Architecture",
                "OAuth Azure Integration",
                "Client Preference System",
                "Assessment Orchestration",
                "MFA Authentication"
            }
        });
    }

    [HttpGet("build-info")]
    public IActionResult GetBuildInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();

        return Ok(new
        {
            AssemblyVersion = assembly.GetName().Version?.ToString(),
            FileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version,
            InformationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            BuildDate = GetBuildDate(),
            TargetFramework = assembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()?.FrameworkName
        });
    }

    private static DateTime GetBuildDate()
    {
        // Get build date from assembly
        var assembly = Assembly.GetExecutingAssembly();
        var location = assembly.Location;

        if (!string.IsNullOrEmpty(location) && System.IO.File.Exists(location))
        {
            return System.IO.File.GetLastWriteTime(location);
        }

        return DateTime.UtcNow;
    }
}