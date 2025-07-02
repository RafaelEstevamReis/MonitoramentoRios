namespace Web.Data.Controllers;

using Microsoft.AspNetCore.Mvc;

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
    }
}
