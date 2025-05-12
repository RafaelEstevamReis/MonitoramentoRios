namespace Web.Data.Controllers;

using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Web.Data.DAO;

[ApiController]
[Route("maintenance")]
public class Maintenance : ControllerBase
{
    internal static string ApiKey = "";

    private readonly DB db;
    public Maintenance(DB db)
    {
        this.db = db;
    }

    [HttpPost("purgeHourly")]
    public IActionResult PurgeHourlyData(string apiKey, int hourKey)
    {
        if (!checkKey(apiKey)) return Unauthorized();

        db.RemoverDadosAgregados(hourKey);

        return Ok();
    }

    [HttpPost("nullifyWaterLevel")]
    public IActionResult NullifyWaterLevel(string apiKey, string estacao, decimal aboveLimit, int limit)
    {
        if (!checkKey(apiKey)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(estacao))
        {
            return BadRequest($"'{nameof(estacao)}' cannot be null or whitespace.");
        }

        var ids = db.ListarDados(estacao, limit)
                    .Where(o => o.NivelRio != null && o.NivelRio > aboveLimit)
                    .Select(o => o.Id)
                    .ToArray();

        db.AnularDadosNivel(estacao, ids);

        return Ok();
    }

    private static bool checkKey(string requestApiKey)
    {
        if (string.IsNullOrWhiteSpace(ApiKey)) return false; // Não tem, não acessa
        if (string.IsNullOrWhiteSpace(requestApiKey)) return false;

        return ApiKey.Equals(requestApiKey);
    }
}
