namespace Web.Data.Controllers;

using Microsoft.AspNetCore.Mvc;
using Simple.DatabaseWrapper.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using Web.Data.DAO;

[ApiController]
[Route("estacoes")]
public class EstacoesController : ControllerBase
{
    private static Dictionary<string,string> dicEstacoes = [];
    private readonly DB db;

    public EstacoesController(DB db)
    {
        this.db = db;
    }

    [HttpGet("dados")]
    public IEnumerable<DadosColetados> ListarDados(string? estacao = null, int limit = 25)
    {
        var lst = db.ListarDados(estacao, limit)
                    .Select(o => Simple.DatabaseWrapper.DataClone.CopyWithSerialization<DadosColetados>(o))
                    .ToArray();

        if (lst.Any(i => !dicEstacoes.ContainsKey(i.Estacao))) atualizaEstacoes(db);

        foreach(var i in  lst)
        {
            if (dicEstacoes.TryGetValue(i.Estacao, out string? value)) i.NomeEstacao = value;
            if(i.TemperaturaInterna.HasValue) i.TemperaturaInterna = Math.Round(i.TemperaturaInterna ?? 0, 1);
            i.RawData = string.Empty;
        }
        return lst;
    }
    [HttpGet("ultimos")]
    public IEnumerable<DadosColetados> ListarRecentes()
    {
        var lst = db.ListarDados(estacao: null, limit: 50) // 50 últimas
                    .Select(Simple.DatabaseWrapper.DataClone.CopyWithSerialization<DadosColetados>)
                    .GroupBy(o => o.Estacao)
                    .Select(o => o.OrderByDescending(k => k.RecebidoUTC).First())
                    .OrderBy(o => o.NomeEstacao)
                    .ToArray();

        if (lst.Any(i => !dicEstacoes.ContainsKey(i.Estacao))) atualizaEstacoes(db);

        foreach (var i in lst)
        {
            if (dicEstacoes.TryGetValue(i.Estacao, out string? value)) i.NomeEstacao = value;
        }
        return lst;
    }

    private static void atualizaEstacoes(DB db)
    {
        dicEstacoes = db.ListarEstacoes()
                        .ToDictionary(o => o.Estacao, o => o.NomeEstacao);
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

    public class DadosColetados
    {
        public DateTime RecebidoUTC { get; set; }
        public string Estacao { get; set; } = string.Empty;
        public string NomeEstacao { get; set; } = string.Empty;
        // Dados Internos
        public DateTime DataHoraDadosUTC { get; set; }
        public decimal? ForcaSinal { get; set; }
        public decimal? TemperaturaInterna { get; set; }
        public decimal? TensaoBateria { get; set; }
        // Medições
        public decimal? TemperaturaAr { get; set; }
        public decimal? UmidadeAr { get; set; }
        public decimal? PressaoAr { get; set; }
        public decimal? Precipitacao { get; set; }
        public decimal? NivelRio { get; set; }
        public decimal? NivelRio_RAW { get; set; }
        public string RawData { get; set; } = string.Empty;
    }
    public class DadosEstacao
    {
        public string NomeResponsavel { get; set; } = string.Empty;
        public string NomeEstacao { get; set; } = string.Empty;
    }
}
