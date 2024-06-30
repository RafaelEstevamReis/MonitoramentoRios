namespace Web.Data.Controllers;

using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Web.Data.DAO;

[ApiController]
[Route("[controller]")]
public class UpController : ControllerBase
{
    private readonly DB db;

    public UpController(DB db)
    {
        this.db = db;
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

        if (!db.IsValidKey(apiKey)) return Unauthorized("Invalid Key");

        string estacao = Helpers.ApiToEstacao(apiKey);

        var d = new DAO.DBModels.TBDadosEstacoes
        {
            // Base
            Id = 0,
            RecebidoUTC = DateTime.UtcNow,
            Estacao = estacao,
            RawData = rawJson,
            IP_Origem = ipOrigem,
            // Internos
            DataHoraDadosUTC = dados.DataHoraDadosUTC ?? DateTime.UtcNow,
            TemperaturaInterna = dados.TemperaturaInterna,
            TensaoBateria = dados.TensaoBateria,
            // Medições
            TemperaturaAr = dados.TemperaturaAr,
            UmidadeAr = dados.UmidadeAr,
            PressaoAr = dados.PressaoAr,
            NivelRio = dados.NivelRio,
            NivelRio_RAW = dados.NivelRio_RAW,
        };
        db.Registra(d);

        return Ok();
    }
    private static string getIP(Microsoft.AspNetCore.Http.HttpRequest request)
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
        public decimal? TemperaturaInterna { get; set; }
        public decimal? TensaoBateria { get; set; }
        /// <summary>
        /// Data que os dados foram gerados segundo a placa
        /// </summary>
        public DateTime? DataHoraDadosUTC { get; set; }
        // Medições
        public decimal? TemperaturaAr { get; set; }
        public decimal? UmidadeAr { get; set; }
        public decimal? PressaoAr { get; set; }
        /// <summary>
        /// Medição calibrada
        /// </summary>
        public decimal? NivelRio { get; set; }
        /// <summary>
        ///  Medição direta sem calibração ou ajuste
        /// </summary>
        public decimal? NivelRio_RAW { get; set; }
    }
}
