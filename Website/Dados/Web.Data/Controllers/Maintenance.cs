namespace Web.Data.Controllers;

using Microsoft.AspNetCore.Mvc;
using System;
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

    [HttpPost("getCurrentHourKey")]
    public IActionResult CurrentHourKey(string apiKey)
    {
        if (!checkKey(apiKey)) return Unauthorized();

        var dtNow = DateTime.UtcNow;
        var hourKey = (int)(dtNow - DateTime.UnixEpoch).TotalHours;

        return Ok(new
        {
            dtNow,
            hourKey
        });
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
    [HttpPost("nova")]
    public IActionResult NovaEstacao([FromQuery] string apiKey, DadosNovaEstacao dados)
    {
        if (!checkKey(apiKey)) return Unauthorized();
        if (dados is null)
        {
            return BadRequest("Invalid POST");
        }
        if (string.IsNullOrEmpty(dados.NomeResponsavel)) return BadRequest("Invalid POST - NomeResponsavel");
        if (string.IsNullOrEmpty(dados.NomeEstacao)) return BadRequest("Invalid POST - NomeEstacao");

        var guid = Guid.NewGuid();
        var key = guid.ToString();
        var estacao = Helpers.ApiToEstacao(key);
        db.NovaEstacao(new DAO.DBModels.TBEstacoes
        {
            ApiKEY = key,
            Estacao = estacao,
            NomeResponsavel = dados.NomeResponsavel,
            NomeEstacao = dados.NomeEstacao,
        });
        return Ok(new
        {
            ApiKey = key,
            IdEstacao = estacao,
        });
    }
    [HttpPost("editar")]
    public IActionResult EditarEstacao([FromQuery] string apiKey, DadosEditarEstacao dados)
    {
        if (!checkKey(apiKey)) return Unauthorized();
        if (dados is null)
        {
            return BadRequest("Invalid POST");
        }
        if (string.IsNullOrEmpty(dados.NomeEstacao)) return BadRequest("Invalid POST - NomeEstacao");

        var estacao = db.ObterDadosCompletosEstacao(dados.IdEstacao);
        if (estacao == null) return BadRequest("Estacao inválida");

        estacao.NomeEstacao = dados.NomeEstacao;
        if (!string.IsNullOrEmpty(dados.NomeResponsavel)) estacao.NomeResponsavel = dados.NomeResponsavel;

        if (dados.ResetarApiKey)
        {
            var guid = Guid.NewGuid();
            var key = guid.ToString();
            string novoId = Helpers.ApiToEstacao(apiKey);

            estacao.ApiKEY = key;
            estacao.Estacao = novoId;
        }

        db.EditaEstacao(estacao);
        return Ok(estacao);
    }

    [HttpGet("listarExterna")]
    public IActionResult ListarExterna(string apiKey)
    {
        if (!checkKey(apiKey)) return Unauthorized();
        return Ok(db.ListarCatalogarExternas());
    }
    [HttpPost("cadastrarExterna")]
    public IActionResult CadastraExterna([FromQuery] string apiKey, DAO.DBModels.TBCatalogarExternas dados)
    {
        if (!checkKey(apiKey)) return Unauthorized();

        db.CadastraAtualizaExterna(dados);
        return Ok();
    }

    private static bool checkKey(string requestApiKey)
    {
        if (string.IsNullOrWhiteSpace(ApiKey)) return false; // Não tem, não acessa
        if (string.IsNullOrWhiteSpace(requestApiKey)) return false;

        return ApiKey.Equals(requestApiKey);
    }


    public class DadosNovaEstacao
    {
        public string NomeResponsavel { get; set; } = string.Empty;
        public string NomeEstacao { get; set; } = string.Empty;
    }
    public class DadosEditarEstacao
    {
        public string IdEstacao { get; set; } = string.Empty;
        public string? NomeResponsavel { get; set; } = null;
        public string NomeEstacao { get; set; } = string.Empty;
        public bool ResetarApiKey { get; set; } = false;
    }
}
