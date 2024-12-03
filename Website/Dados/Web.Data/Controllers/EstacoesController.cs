namespace Web.Data.Controllers;

using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using Web.Data.DAO;

[ApiController]
[Route("estacoes")]
public class EstacoesController : ControllerBase
{
    private static Dictionary<string, string> dicEstacoesNomesId = [];
    private static Dictionary<string, string> dicEstacoes = [];
    private readonly DB db;

    public EstacoesController(DB db)
    {
        this.db = db;
    }

    [HttpGet("dados")]
    public IEnumerable<DadosColetados> ListarDados(string? estacao = null, int limit = 64)
    {
        if (estacao != null && estacao.Length == 9 && estacao[4] == '-')
        {
            if (dicEstacoesNomesId.TryGetValue(estacao, out string? value) && value != null)
            {
                estacao = value;
            }
            else
            {
                var estacaoBusca = db.ListarEstacoes()
                                    .Where(e => e.NomeEstacao.Equals(estacao, StringComparison.InvariantCultureIgnoreCase))
                                    .OrderByDescending(o => o.Id)
                                    //.FirstOrDefault()
                                    ;
                // Não mudar ordem
                if (estacaoBusca != null)
                {
                    dicEstacoesNomesId[estacao] = estacaoBusca.FirstOrDefault()?.Estacao ?? "";
                    estacao = estacaoBusca.FirstOrDefault()?.Estacao;
                }
            }
        }

        var lst = db.ListarDados(estacao, limit)
                    .Select(o => Simple.DatabaseWrapper.DataClone.CopyWithSerialization<DadosColetados>(o))
                    .ToArray();

        if (lst.Any(i => !dicEstacoes.ContainsKey(i.Estacao))) atualizaEstacoes(db);

        foreach (var i in lst)
        {
            if (dicEstacoes.TryGetValue(i.Estacao, out string? value)) i.NomeEstacao = value;
            if (i.TemperaturaInterna.HasValue) i.TemperaturaInterna = Math.Round(i.TemperaturaInterna ?? 0, 1);
            i.RawData = string.Empty;
        }
        return lst;
    }
    [HttpGet("ultimos")]
    public IEnumerable<DadosColetados> ListarRecentes()
    {
        var lst = db.ListarDados(estacao: null, limit: 64) // 50 últimas
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

    [HttpGet("agregado")]
    public DadosAgregados AgregarDados(string estacao, int hour = 8)
    {
        var lst = db.ListarDados(estacao, hour * 60)
                    .Where(o => (DateTime.UtcNow - o.DataHoraDadosUTC).TotalHours <= hour)
                    .ToArray();

        return new DadosAgregados
        {
            Avg = new DadosColetados
            {
                ForcaSinal = lst.Average(o=> o.ForcaSinal),
                NivelRio = lst.Average(o => o.NivelRio),
                PercentBateria = lst.Average(o => o.PercentBateria),
                Precipitacao = lst.Average(o => o.Precipitacao),
                PressaoAr = lst.Average(o => o.PressaoAr),
                TemperaturaAr = lst.Average(o => o.TemperaturaAr),
                UmidadeAr = lst.Average(o => o.UmidadeAr),
                TemperaturaInterna = lst.Average(o => o.TemperaturaInterna),
                TensaoBateria = lst.Average(o => o.TensaoBateria),
            },
            Max = new DadosColetados
            {
                ForcaSinal = lst.Max(o => o.ForcaSinal),
                NivelRio = lst.Max(o => o.NivelRio),
                PercentBateria = lst.Max(o => o.PercentBateria),
                Precipitacao = lst.Max(o => o.Precipitacao),
                PressaoAr = lst.Max(o => o.PressaoAr),
                TemperaturaAr = lst.Max(o => o.TemperaturaAr),
                UmidadeAr = lst.Max(o => o.UmidadeAr),
                TemperaturaInterna = lst.Max(o => o.TemperaturaInterna),
                TensaoBateria = lst.Max(o => o.TensaoBateria),
            },
            Min = new DadosColetados
            {
                ForcaSinal = lst.Min(o => o.ForcaSinal),
                NivelRio = lst.Min(o => o.NivelRio),
                PercentBateria = lst.Min(o => o.PercentBateria),
                Precipitacao = lst.Min(o => o.Precipitacao),
                PressaoAr = lst.Min(o => o.PressaoAr),
                TemperaturaAr = lst.Min(o => o.TemperaturaAr),
                UmidadeAr = lst.Min(o => o.UmidadeAr),
                TemperaturaInterna = lst.Min(o => o.TemperaturaInterna),
                TensaoBateria = lst.Min(o => o.TensaoBateria),
            },
        };
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
        public decimal? PercentBateria { get; set; }
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
    public class DadosAgregados
    {
        public DadosColetados Min { get; set; }
        public DadosColetados Max { get; set; }
        public DadosColetados Avg { get; set; }

    }
}
