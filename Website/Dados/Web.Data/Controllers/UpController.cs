namespace Web.Data.Controllers;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Web.Data.DAO;

[ApiController]
[Route("[controller]")]
public class UpController : ControllerBase
{
    static CalibracaoDigital.ICalibracaoDigital[] calibracoes;
    static UpController()
    {
        var tIC = typeof(CalibracaoDigital.ICalibracaoDigital);
        var thisAsm = Assembly.GetExecutingAssembly();
        var types = thisAsm.GetTypes();
        var tCal = types.Where(t => tIC.IsAssignableFrom(t)) // todas as calibrações
                        .Where(t => !t.IsInterface) // remove a interface
                        .ToArray();

        calibracoes = tCal.Select(o => (CalibracaoDigital.ICalibracaoDigital)(Activator.CreateInstance(o) ?? throw new InvalidOperationException()))
                          .Where(o => o.ValidadeFimUTC > DateTime.UtcNow) // Já venceu?
                          .ToArray();

        Log.Logger.Information("Calibrações Carregadas: {qtd} {lista}",
                               calibracoes.Length,
                               string.Join(", ", calibracoes.Select(ajustaNomeLog))
                               );
    }
    static string ajustaNomeLog(CalibracaoDigital.ICalibracaoDigital o)
    {
        return o.GetType().FullName?.Replace("Web.Data.CalibracaoDigital.", "") ?? o.GetType().Name;
    }

    private readonly DB db;
    private readonly ILogger log;

    public UpController(DB db, ILogger log)
    {
        this.db = db;
        this.log = log;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload()
    {
        var apiKey = Request.Headers["x-key"].ToString() ?? "";

        string json;
        using (var reader = new StreamReader(Request.Body))
        {
            json = await reader.ReadToEndAsync();
        }
        var dados = JsonConvert.DeserializeObject<UploadData>(json);

        var ip = getIP(Request);
        return registraDados(dados, apiKey, json, ip);
    }

    [HttpPost("upload_swagger")]
    public IActionResult Upload_Test([FromBody] UploadData dados, [FromQuery] string apiKey)
    {
        var stringObject = JsonConvert.SerializeObject(dados);
        var ip = getIP(Request);
        return registraDados(dados, apiKey, stringObject, ip);
    }

    private IActionResult registraDados(UploadData dados, string apiKey, string rawJson, string ipOrigem)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return BadRequest("Invalid KEY [0]");
        if (apiKey.Length < 16) return BadRequest("Invalid KEY [1]");

        if (!db.IsValidKey(apiKey))
        {
            log.Warning("Invalid KEY: " + apiKey);
            return Unauthorized("Invalid Key");
        }

        string estacao = Helpers.ApiToEstacao(apiKey);
        // Faz por baixo
        _ = Task.Run(() => finalizaGravacaoDados(dados, rawJson, ipOrigem, estacao));
        // Retorna resposta rápido
        return Ok();
    }

    private void finalizaGravacaoDados(UploadData dados, string rawJson, string ipOrigem, string estacao)
        => sFinalizaGravacaoDados(db, log, dados, rawJson, ipOrigem, estacao);
    internal static void sFinalizaGravacaoDados(DB db, ILogger log, UploadData dados, string rawJson, string ipOrigem, string estacao)
    {
        if (dados.nonce.HasValue && dados.nonce > 0)
        {
            var noncesRecentes = db.ListarNoncesRecentes(estacao);
            if(noncesRecentes.Contains(dados.nonce.Value))
            {
                log.Information("[CTRL] NonceSkip {estacao} r={nonce} ", estacao, dados.nonce);
                return;
            }
        }

        var roj = JsonNode.Parse(rawJson) ?? JsonNode.Parse("{}");
        List<string> lstCal = [];
        try
        {
            if (calibracoes.Length > 0) // Ainda não precisa de um dicionário
            {
                var utcNow = DateTime.UtcNow;
                foreach (var cal in calibracoes)
                {
                    // Estação correta?
                    if (cal.Estacao != estacao) continue;
                    // Válido?
                    if (cal.ValidadeInicioUTC > utcNow) continue;
                    if (cal.ValidadeFimUTC < utcNow) continue;

                    if (!cal.ProcessaDados(dados, roj)) continue;

                    lstCal.Add(cal.GetType().Name); // Marca como feito
                }
            }
        }
        catch (Exception ex)
        {
            log.Error(ex, "UpController:sFinalizaGravacaoDados Falha ao listar calibrações");
        }

        if (lstCal.Count > 0)
        {
            log.Information("[UpController] Calibração {estacao} Lista: {@lista}", estacao, lstCal);
            var calArr = new JsonArray();
            foreach (var cal in lstCal) calArr.Add(cal);
            roj["CAL"] = calArr;

            rawJson = roj.ToJsonString();
        }

        // Corrige valores
        if (dados.ForcaSinal == 0) dados.ForcaSinal = null;
        if (dados.PercentBateria > 100) dados.PercentBateria = 100;
        // Se tem Temp2 e Temp1 não veio, utiliza ele
        if (dados.TemperaturaAr == null)
        {
            if (roj?["Temperatura2"] != null)
            {
                dados.TemperaturaAr = (decimal?)roj["Temperatura2"];
            }
        }

        string? imgPath = null;
        if (dados.pic_b64 != null)
        {
            var imgBytes = Convert.FromBase64String(dados.pic_b64);
            imgPath = salvaImagem(log, estacao, imgBytes, ipOrigem);
            rawJson = rawJson.Replace(dados.pic_b64, "[PIC]");
        }

        var origem = DAO.DBModels.TBDadosEstacoes.DataSource.Internet;
        if (ipOrigem != null)
        {
            if (ipOrigem.StartsWith("192")) origem = DAO.DBModels.TBDadosEstacoes.DataSource.Lan;
            if (ipOrigem.StartsWith("EX.")) origem = DAO.DBModels.TBDadosEstacoes.DataSource.Externo;
        }

        var d = new DAO.DBModels.TBDadosEstacoes
        {
            // Base
            Id = 0,
            RecebidoUTC = DateTime.UtcNow,
            Estacao = estacao,
            type = dados.type,
            RawData = rawJson,
            IP_Origem = ipOrigem,
            Source = origem,
            // Internos
            ForcaSinal = dados.ForcaSinal,
            DataHoraDadosUTC = dados.DataHoraDadosUTC ?? DateTime.UtcNow,
            TemperaturaInterna = dados.TemperaturaInterna,
            TensaoBateria = dados.TensaoBateria,
            PercentBateria = dados.PercentBateria,
            // Medições
            TemperaturaAr = dados.TemperaturaAr,
            UmidadeAr = dados.UmidadeAr,
            PressaoAr = dados.PressaoAr,
            Precipitacao = dados.Precipitacao,
            Precipitacao10min = dados.Precipitacao10m,
            PrecipitacaoTotal = dados.PrecipitacaoTotal,
            NivelRio = dados.NivelRio,
            NivelRio_RAW = dados.NivelRio_RAW,
            ImgPath = imgPath,
            Nonce = dados.nonce ?? 0
        };
        db.Registra(d);

        db.AtualizaEstacao(estacao, roj?["mac"]?.ToString() ?? "", roj?["ip"]?.ToString() ?? "");
    }

    private static string salvaImagem(ILogger log, string estacao, byte[] rawData, string ipOrigem)
    {
        var dir = Path.Combine("data", "img", estacao);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        var filePath = Path.Combine(dir, $"{estacao}-{DateTime.UtcNow:yyyyMMddHHmmss}.jpg");

        log.Information($"Image from {estacao} saved at {new FileInfo(filePath).FullName} Size: {rawData.Length} ");
        // Salva a imagem no caminho definido
        System.IO.File.WriteAllBytes(filePath, rawData);

        return filePath;
    }
    private static string getIP(HttpRequest request)
    {
        var ip = request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (string.IsNullOrEmpty(ip))
        {
            ip = request.HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString();
        }
        return ip ?? "";
    }

    public class UploadData
    {
        // Dados Internos
        public decimal? ForcaSinal { get; set; }
        public string? SSID { get; set; }
        public string? BSSID { get; set; }
        /// <summary>
        /// Tipo de dispositivo
        /// </summary>
        public string? type { get; set; }
        /// <summary>
        /// Data que os dados foram gerados segundo a placa
        /// </summary>
        public DateTime? DataHoraDadosUTC { get; set; }
        public decimal? TemperaturaInterna { get; set; }
        public decimal? TensaoBateria { get; set; }
        public decimal? PercentBateria { get; set; }
        // Medições
        public decimal? TemperaturaAr { get; set; }
        public decimal? UmidadeAr { get; set; }
        public decimal? PressaoAr { get; set; }
        public decimal? Precipitacao { get; set; }
        public decimal? Precipitacao10m { get; set; }
        public decimal? PrecipitacaoTotal { get; set; }
        /// <summary>
        /// Medição calibrada
        /// </summary>
        public decimal? NivelRio { get; set; }
        /// <summary>
        ///  Medição direta sem calibração ou ajuste
        /// </summary>
        public decimal? NivelRio_RAW { get; set; }
        /// <summary>
        /// Imagem capturada por câmera
        /// </summary>
        public string? pic_b64 { get; set; }
        public int? nonce { get; set; }
    }
}
