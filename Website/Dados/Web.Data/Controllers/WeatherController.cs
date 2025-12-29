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

    [HttpGet()]
    public IActionResult Obter()
    {
        var hora = DateTime.UtcNow.Hour;

        if (hora != currHourCache || wcache.Length == 0)
        {
            wcache = db.ObterWeatherProximasHoras("RSRL").ToArray();
            currHourCache = hora;
        }

        return Ok(wcache);
    }
    [HttpGet("blocks")]
    public IActionResult ObterAgrupado()
    {
        var utc = DateTime.UtcNow;
        var hora = utc.Hour;

        if (hora != currHourCache || wcache.Length == 0)
        {
            wcache = db.ObterWeatherProximasHoras("RSRL").ToArray();
            currHourCache = hora;
        }
        int tz = -3;
        var utcHour = utc.Date.AddHours(hora);
        var group = wcache.Where(o => o.ForecastUTC >= utcHour)
                          .Chunk(3)
                          .Take(4)
                          .Select(g =>
                          {
                              return new WeatherAgrupado
                              {
                                  HorarioLocal = g.First().ForecastUTC
                                                  .AddHours(tz).Hour.ToString("00") + "h",
                                  Temperatura = g.Max(o => o.Temperatura),
                                  ventoVelocidade = g.Max(o => o.VentoVelocidade),
                                  Precipitacao = g.Sum(o => o.Precipitacao),
                                  ColetaUTC = g.First().ColetaUTC,
                              };
                          })
                          .ToArray()
                          ;

        return Ok(group);
    }

    [HttpGet("ext")]
    public IActionResult ObterExtendido()
    {
        return Ok(db.ObterWeatherEstendido("RSRL"));
    }

    public record WeatherAgrupado
    {
        public string HorarioLocal { get; set; }
        public DateTime ColetaUTC { get; set; }
        public decimal Temperatura { get; set; }
        public decimal ventoVelocidade { get; set; }
        public decimal Precipitacao { get; set; }
    }

}
