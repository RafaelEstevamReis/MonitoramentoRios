namespace Web.Data.Controllers;

using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using Web.Data.DAO;

[ApiController]
[Route("[controller]")]
public class Estacoes : ControllerBase
{
    private readonly DB db;

    public Estacoes(DB db)
    {
        this.db = db;
    }

    [HttpGet("dados")]
    public IEnumerable<DAO.DBModels.TBDadosEstacoes> ListarDados(string? estacao = null, int limit = 25)
    {
        return db.ListarEstacoes(estacao, limit);
    }

    [HttpPost("nova")]
    public IActionResult NovaEstacao(DadosEstacao dados)
    {
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

    public class DadosEstacao
    {
        public string NomeResponsavel { get; set; } = string.Empty;
        public string NomeEstacao { get; set; } = string.Empty;
    }
}
