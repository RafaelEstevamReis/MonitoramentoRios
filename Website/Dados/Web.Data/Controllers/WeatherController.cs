namespace Web.Data.Controllers;

using Microsoft.AspNetCore.Mvc;
using Serilog;
using System;
using System.Linq;
using Web.Data.DAO;

[ApiController]
[Route("[controller]")]
public class WeatherController : ControllerBase
{
    private readonly DB db;
    private readonly ILogger log;

    static int currHourCache = -1;
    static DAO.DBModels.TBWeather[] wcache = [];


    public WeatherController(DB db, ILogger log)
    {
        this.db = db;
        this.log = log;
    }

    [HttpGet]
    public IActionResult Obter()
    {
        var hora = DateTime.UtcNow.Hour;

        if (hora != currHourCache || wcache.Length == 0)
        {
            wcache = db.ObterWeatherProximasHoras().ToArray();
            currHourCache = hora;
        }

        return Ok(wcache);
    }

    [HttpGet("ext")]
    public IActionResult ObterExtendido()
    {
        return Ok(db.ObterWeatherProximasHoras(hour: 96));
    }

}
