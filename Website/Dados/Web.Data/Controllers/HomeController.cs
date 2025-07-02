namespace Web.Data.Controllers;

using Microsoft.AspNetCore.Mvc;

[ApiController]
public class HomeController : ControllerBase
{
    internal static readonly VersionInfo VERSION = new VersionInfo { Revision = "0000" };

    [HttpGet("/")]
    public IActionResult GetHome()
    {
        return Redirect("/home.html");
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
    }
}
