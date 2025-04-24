namespace Web.Data.Controllers;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Web.Data.DAO;

[ApiController]
[Route("[controller]")]
public class UpController : ControllerBase
{
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
    {
        // Corrige valores
        if (dados.ForcaSinal == 0) dados.ForcaSinal = null;
        if (dados.PercentBateria > 100) dados.PercentBateria = 100;

        string? imgPath = null;
        if (dados.pic_b64 != null)
        {
            var imgBytes = Convert.FromBase64String(dados.pic_b64);
            imgPath = salvaImagem(estacao, imgBytes, ipOrigem);
            rawJson = rawJson.Replace(dados.pic_b64, "[PIC]");
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
            Source = ipOrigem.StartsWith("192") ? DAO.DBModels.TBDadosEstacoes.DataSource.Lan : DAO.DBModels.TBDadosEstacoes.DataSource.Internet,
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
            NivelRio = dados.NivelRio,
            NivelRio_RAW = dados.NivelRio_RAW,
            ImgPath = imgPath,
            Nonce = dados.nonce ?? 0
        };
        db.Registra(d);

        var roj = JsonNode.Parse(rawJson);
        db.AtualizaEstacao(estacao, roj?["mac"]?.ToString() ?? "", roj?["ip"]?.ToString() ?? "");
    }

    private string salvaImagem(string estacao, byte[] rawData, string ipOrigem)
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
