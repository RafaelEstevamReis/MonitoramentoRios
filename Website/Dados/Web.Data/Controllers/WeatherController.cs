namespace Web.Data.Controllers;

using Microsoft.AspNetCore.Mvc;
using Serilog;
using Web.Data.DAO;

[ApiController]
[Route("[controller]")]
public class WeatherController : ControllerBase
{
    private readonly DB db;
    private readonly ILogger log;

    public WeatherController(DB db, ILogger log)
    {
        this.db = db;
        this.log = log;
    }

    [HttpGet]
    public IActionResult Obter()
    {
        return Ok(db.ObterWeatherProximasHoras());
    }

    [HttpGet("dia")]
    public IActionResult ObterHourKey([FromQuery] int hourKey)
    {
        return Ok(db.ObterWeatherDia(hourKey));
    }

}
