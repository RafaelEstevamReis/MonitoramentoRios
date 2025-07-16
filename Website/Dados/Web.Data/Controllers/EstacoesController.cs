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
        // Converte nome da estação para key
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
                    .Select(o => converteDados(estacao ?? o.Estacao, o))
                    .ToArray();

        if (lst.Any(i => !dicEstacoes.ContainsKey(i.Estacao))) atualizaEstacoes(db);

        foreach (var i in lst)
        {
            if (dicEstacoes.TryGetValue(i.Estacao, out string? value)) i.NomeEstacao = value;
            if (i.TemperaturaInterna.HasValue) i.TemperaturaInterna = Math.Round(i.TemperaturaInterna ?? 0, 1);
            i.RawData = null;
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
                    Source = registroMaisRecente.Source,

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
                    Precipitacao10min = registrosOrdenados.FirstOrDefault(r => r.Precipitacao10min.HasValue)?.Precipitacao,
                    NivelRio = registrosOrdenados.FirstOrDefault(r => r.NivelRio.HasValue)?.NivelRio,
                    NivelRio_RAW = registrosOrdenados.FirstOrDefault(r => r.NivelRio_RAW.HasValue)?.NivelRio_RAW,

                    // Dados brutos
                    //RawData = registroMaisRecente.RawData // Remove
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
        var horaAgora = (int)(DateTime.UtcNow - DateTime.UnixEpoch).TotalHours;

        var range = Enumerable.Range(horaAgora - hour, hour).ToArray();
        var lst = db.AgregadoHoraRange(estacao, range);

        decimal? pTotal = null;
        if (lst.Length > 0) pTotal = lst.Where(o => o.PrecipitacaoTotal_Hora != null).Sum(o => o.PrecipitacaoTotal_Hora);

        return new DadosAgregados
        {
            ForcaSinal_AVG = lst.Average(o => o.ForcaSinal_AVG),
            ForcaSinal_MIN = lst.Min(o => o.ForcaSinal_MIN),
            ForcaSinal_MAX = lst.Min(o => o.ForcaSinal_MAX),

            TensaoBateria_AVG = lst.Average(o => o.TensaoBateria_AVG),
            TensaoBateria_MIN = lst.Min(o => o.TensaoBateria_MIN),
            TensaoBateria_MAX = lst.Max(o => o.TensaoBateria_MAX),

            PercentBateria_AVG = lst.Average(o => o.PercentBateria_AVG),
            PercentBateria_MIN = lst.Min(o => o.PercentBateria_MIN),
            PercentBateria_MAX = lst.Max(o => o.PercentBateria_MAX),

            TemperaturaInterna_AVG = lst.Average(o => o.TemperaturaInterna_AVG),
            TemperaturaInterna_MIN = lst.Min(o => o.TemperaturaInterna_MIN),
            TemperaturaInterna_MAX = lst.Max(o => o.TemperaturaInterna_MAX),

            TemperaturaAr_AVG = lst.Average(o => o.TemperaturaAr_AVG),
            TemperaturaAr_MIN = lst.Min(o => o.TemperaturaAr_MIN),
            TemperaturaAr_MAX = lst.Max(o => o.TemperaturaAr_MAX),

            UmidadeAr_AVG = lst.Average(o => o.UmidadeAr_AVG),
            UmidadeAr_MIN = lst.Min(o => o.UmidadeAr_MIN),
            UmidadeAr_MAX = lst.Max(o => o.UmidadeAr_MAX),

            PressaoAr_AVG = lst.Average(o => o.PressaoAr_AVG),
            PressaoAr_MIN = lst.Min(o => o.PressaoAr_MIN),
            PressaoAr_MAX = lst.Max(o => o.PressaoAr_MAX),

            NivelRio_AVG = lst.Average(o => o.NivelRio_AVG),
            NivelRio_MIN = lst.Min(o => o.NivelRio_MIN),
            NivelRio_MAX = lst.Max(o => o.NivelRio_MAX),

            PrecipitacaoTotal_Hora = lst.Sum(o => o.PrecipitacaoTotal_Hora),
            PrecipitacaoTotal_MIN = lst.Min(o => o.PrecipitacaoTotal_MIN),
            PrecipitacaoTotal_MAX = lst.Max(o => o.PrecipitacaoTotal_MAX),
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
        var resp = db.AgregadoHoraRange(estacao, range)
                     .Where(o => o.DataCount > 0);
        return Ok(resp);
    }

    private static void atualizaEstacoes(DB db)
    {
        dicEstacoes = db.ListarEstacoes()
                        .ToDictionary(o => o.Estacao, o => o.NomeEstacao);
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
            LA = $"*.{e.LA}",
            Serial = e.Serial.ToString("x4")[^4..]
        }));
    }
    private DadosColetados? converteDados(DAO.DBModels.TBEstacoes e, DAO.DBModels.TBDadosEstacoes? dados)
        => converteDados(e.NomeEstacao, dados);
    private DadosColetados? converteDados(string nomeEstacao, DAO.DBModels.TBDadosEstacoes? dados)
    {
        if (dados == null) return null;
        var k = Simple.DatabaseWrapper.DataClone.MapModel<DAO.DBModels.TBDadosEstacoes, DadosColetados>(dados);
        k.NomeEstacao = nomeEstacao;
        k.Precipitacao10min = dados.Precipitacao10min;

        return k;
    }

    public class DadosColetados
    {
        public DateTime RecebidoUTC { get; set; }
        public string Estacao { get; set; } = string.Empty;
        public string NomeEstacao { get; set; } = string.Empty;
        public string type { get; set; } = string.Empty;
        public int Source { get; set; }
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
        public decimal? Precipitacao10min { get; set; }
        public decimal? PrecipitacaoTotal { get; set; }
        public decimal? NivelRio { get; set; }
        public decimal? NivelRio_RAW { get; set; }
        public string? RawData { get; set; } = string.Empty;
    }
    public class DadosEstacao
    {
        public string Estacao { get; set; } = string.Empty;
        public string NomeResponsavel { get; set; } = string.Empty;
        public string NomeEstacao { get; set; } = string.Empty;
        public DadosColetados? UltimoEnvio { get; set; }
        public string Serial { get; set; } = string.Empty;
        public string LA { get; set; } = string.Empty;
    }
    public class DadosAgregados
    {
        public decimal? ForcaSinal_MAX { get; set; }
        public decimal? ForcaSinal_MIN { get; set; }
        public decimal? ForcaSinal_AVG { get; set; }

        public decimal? TemperaturaInterna_MAX { get; set; }
        public decimal? TemperaturaInterna_MIN { get; set; }
        public decimal? TemperaturaInterna_AVG { get; set; }

        public decimal? TensaoBateria_MAX { get; set; }
        public decimal? TensaoBateria_MIN { get; set; }
        public decimal? TensaoBateria_AVG { get; set; }

        public decimal? PercentBateria_MAX { get; set; }
        public decimal? PercentBateria_MIN { get; set; }
        public decimal? PercentBateria_AVG { get; set; }

        // Medições
        public decimal? TemperaturaAr_MAX { get; set; }
        public decimal? TemperaturaAr_MIN { get; set; }
        public decimal? TemperaturaAr_AVG { get; set; }

        public decimal? UmidadeAr_MAX { get; set; }
        public decimal? UmidadeAr_MIN { get; set; }
        public decimal? UmidadeAr_AVG { get; set; }

        public decimal? PressaoAr_MAX { get; set; }
        public decimal? PressaoAr_MIN { get; set; }
        public decimal? PressaoAr_AVG { get; set; }

        public decimal? PrecipitacaoTotal_MAX { get; set; }
        public decimal? PrecipitacaoTotal_MIN { get; set; }
        public decimal? PrecipitacaoTotal_Hora { get; set; }

        public decimal? NivelRio_MAX { get; set; }
        public decimal? NivelRio_MIN { get; set; }
        public decimal? NivelRio_AVG { get; set; }
    }
}
