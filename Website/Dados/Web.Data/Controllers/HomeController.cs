namespace Web.Data.Controllers;

using Microsoft.AspNetCore.Mvc;
using Serilog;
using System;
using System.Diagnostics;

[ApiController]
public class HomeController : ControllerBase
{
    internal static readonly VersionInfo VERSION = new VersionInfo { Revision = "0000" };

    [HttpGet("/home.html")] // Corrige antigo home para INDEX
    public IActionResult GetIndex()
    {
        return Redirect("/index.html");
    }

    [HttpGet("robots.txt")]
    public IActionResult GetRobotsTxt()
    {
        return Ok("");
    }

    [HttpGet("version")]
    public IActionResult GetVersionInfo()
    {
        return Ok(VERSION);
    }

    public record VersionInfo
    {
        public string Revision { get; set; } = string.Empty;
        public string Enviroment { get; set; } = Environment.OSVersion.Platform.ToString();
        public string Server { get; set; } = GetServerName();
        public bool IsAZ { get; set; } = IsRunningAzure();

        private static bool IsRunningAzure()
        {
            string? websiteSiteName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
            string? websiteSku = Environment.GetEnvironmentVariable("WEBSITE_SKU");
            string? websiteHostname = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");

            if (!string.IsNullOrEmpty(websiteSiteName))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private static string GetServerName()
        {
            var procName = Process.GetCurrentProcess().ProcessName;
            Log.Logger.Warning("[INT] Process: {name} ", procName);
            return procName switch
            {
                "w3wp.exe" => "I6", // IIS6
                "iisexpress.exe" => "IEX", // IISExpress
                "dotnet.exe" => "Kestrel",
                "Web.Data" => "http",
                _ => "UNK",
            };
        }
    }
}
