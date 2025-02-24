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
        // Obtenha os dados da base de dados
        var dados = db.ListarDados(estacao: null, limit: 64) // Carrega as últimas entradas
                      .Select(Simple.DatabaseWrapper.DataClone.CopyWithSerialization<DadosColetados>)
                      .ToList();

        // Atualiza os nomes das estações, se necessário
        if (dados.Any(i => !dicEstacoes.ContainsKey(i.Estacao)))
        {
            atualizaEstacoes(db);
        }

        // Cria um dicionário para armazenar os dados mais recentes por estação
        var dadosCompletosPorEstacao = dados
            .GroupBy(o => o.Estacao)
            .Select(grupo =>
            {
                // Ordena os registros do grupo por data (mais recente primeiro)
                var registrosOrdenados = grupo.OrderByDescending(o => o.RecebidoUTC).ToList();

                // Seleciona o registro mais recente
                var registroMaisRecente = registrosOrdenados.First();

                // Preenche as propriedades com os valores mais recentes não nulos
                var dadosCompletos = new DadosColetados
                {
                    RecebidoUTC = registroMaisRecente.RecebidoUTC,
                    Estacao = registroMaisRecente.Estacao,
                    NomeEstacao = registroMaisRecente.NomeEstacao,
                    DataHoraDadosUTC = registroMaisRecente.DataHoraDadosUTC,

                    // Dados internos
                    ForcaSinal = registrosOrdenados.FirstOrDefault(r => r.ForcaSinal.HasValue)?.ForcaSinal,
                    TemperaturaInterna = registrosOrdenados.FirstOrDefault(r => r.TemperaturaInterna.HasValue)?.TemperaturaInterna,
                    TensaoBateria = registrosOrdenados.FirstOrDefault(r => r.TensaoBateria.HasValue)?.TensaoBateria,
                    PercentBateria = registrosOrdenados.FirstOrDefault(r => r.PercentBateria.HasValue)?.PercentBateria,

                    // Medições
                    TemperaturaAr = registrosOrdenados.FirstOrDefault(r => r.TemperaturaAr.HasValue)?.TemperaturaAr,
                    UmidadeAr = registrosOrdenados.FirstOrDefault(r => r.UmidadeAr.HasValue)?.UmidadeAr,
                    PressaoAr = registrosOrdenados.FirstOrDefault(r => r.PressaoAr.HasValue)?.PressaoAr,
                    Precipitacao = registrosOrdenados.FirstOrDefault(r => r.Precipitacao.HasValue)?.Precipitacao,
                    NivelRio = registrosOrdenados.FirstOrDefault(r => r.NivelRio.HasValue)?.NivelRio,
                    NivelRio_RAW = registrosOrdenados.FirstOrDefault(r => r.NivelRio_RAW.HasValue)?.NivelRio_RAW,

                    // Dados brutos
                    RawData = registroMaisRecente.RawData
                };

                // Atualiza o nome da estação, se disponível
                if (dicEstacoes.TryGetValue(dadosCompletos.Estacao, out string? nomeEstacao))
                {
                    dadosCompletos.NomeEstacao = nomeEstacao;
                }

                return dadosCompletos;
            })
            .OrderBy(o => o.NomeEstacao) // Ordena pelo nome da estação
            .ToArray();

        return dadosCompletosPorEstacao;
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
                ForcaSinal = lst.Average(o => o.ForcaSinal),
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

    [HttpGet("hourly")]
    public IActionResult AgregarHora(string estacao, int hourSpan)
    {
        var agr = db.AgregadoHora(estacao, hourSpan);
        if (agr == null) return NoContent();
        return Ok(agr);
    }

    [HttpGet("lastHourly")]
    public IActionResult FaixaHora(string estacao, int lastHours = 8)
    {
        var horaAgora = (int)(DateTime.UtcNow - DateTime.UnixEpoch).TotalHours;

        var range = Enumerable.Range(horaAgora - lastHours, lastHours).ToArray();
        return Ok(range.Select(h => db.AgregadoHora(estacao, h)));
    }

    private static void atualizaEstacoes(DB db)
    {
        dicEstacoes = db.ListarEstacoes()
                        .ToDictionary(o => o.Estacao, o => o.NomeEstacao);
    }

    [NonAction]
    [HttpPost("nova")]
    public IActionResult NovaEstacao(DadosNovaEstacao dados)
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

    [HttpGet("")]
    public IActionResult ListarEstacoes()
    {
        var todas = db.ListarEstacoes();

        return Ok(todas.Select(e => new DadosEstacao
        {
            Estacao = e.Estacao,
            NomeEstacao = e.NomeEstacao,
            NomeResponsavel = e.NomeResponsavel,
            UltimoEnvio = converteDados(e, db.ObterRegistroEstacao(e.UltimoEnvio)),
        }));
    }
    private DadosColetados? converteDados(DAO.DBModels.TBEstacoes e, DAO.DBModels.TBDadosEstacoes? dados)
    {
        if (dados == null) return null;
        var k = Simple.DatabaseWrapper.DataClone.MapModel<DAO.DBModels.TBDadosEstacoes, DadosColetados>(dados);
        k.NomeEstacao = e.NomeEstacao;

        return k;
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
    public class DadosNovaEstacao
    {
        public string NomeResponsavel { get; set; } = string.Empty;
        public string NomeEstacao { get; set; } = string.Empty;
    }
    public class DadosEstacao
    {
        public string Estacao { get; set; } = string.Empty;
        public string NomeResponsavel { get; set; } = string.Empty;
        public string NomeEstacao { get; set; } = string.Empty;
        public DadosColetados? UltimoEnvio { get; set; }
    }
    public class DadosAgregados
    {
        public DadosColetados Min { get; set; }
        public DadosColetados Max { get; set; }
        public DadosColetados Avg { get; set; }
    }
}
