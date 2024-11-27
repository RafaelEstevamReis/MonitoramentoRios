namespace Web.Data.Controllers;

using Microsoft.AspNetCore.Mvc;

[ApiController]
public class HomeController : ControllerBase
{
    [HttpGet("/")]
    public IActionResult Home()
    {
        return Redirect("/dados.html");
    }

    [NonAction]
    [HttpGet("robots.txt")]
    public IActionResult RobotsTxt()
    {
        return Ok("");
    }
}
