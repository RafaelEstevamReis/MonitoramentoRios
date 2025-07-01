namespace Web.Data.BkgWorkers;

using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using Serilog;
using Simple.API;
using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Web.Data.DAO;

public class ExternalArchiver : IHostedService, IDisposable
{
    private readonly ILogger logger;
    private readonly DB db;
    private readonly ClientInfo wlClient;
    private readonly HttpClient httpClient;

    private Timer _timer;
    public ExternalArchiver(DB db, ILogger logger)
    {
        this.db = db;
        this.logger = logger;

        wlClient = new ClientInfo("https://www.weatherlink.com");
        wlClient.SetHeader("x-requested-with", "XMLHttpRequest");

        httpClient = new HttpClient();
    }
    public void Dispose()
    {
        _timer?.Dispose();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        TimeSpan start;
#if DEBUG
        start = TimeSpan.FromSeconds(15);
#else
        start = TimeSpan.FromMinutes(15);
#endif

        logger.Information("[ExternalArchiver] Iniciando serviço de Archive Externo de dados...");
        _timer = new Timer(executaVerificacaoAsync, null, start, TimeSpan.FromMinutes(15));

        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    private async void executaVerificacaoAsync(object? state)
    {
        var externas = db.ListarCatalogarExternas().Where(o => o.Ativo).ToArray();

        foreach (var e in externas)
        {
            try
            {
                await Task.Delay(5000);

                if (e.Origem == DAO.DBModels.TBCatalogarExternas.DataSource.WLink)
                {
                    await catalogarWLink(e);
                }

                if (e.Origem == DAO.DBModels.TBCatalogarExternas.DataSource.CEMADEN)
                {
                    await catalogarCemaden(e);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "[ExternalArchiver] Falha na execução: Estação: {estacao}", e.Estacao);
            }
        }
    }

    private async Task catalogarWLink(DAO.DBModels.TBCatalogarExternas e)
    {
        var recentesEstacao = db.ListarDados(e.Estacao);
        var hsh = recentesEstacao.Select(o => $"{o.Estacao}#{o.DataHoraDadosUTC:yyyyMMddHHmm}").ToHashSet();

        var r = await wlClient.GetAsync<JObject>($"/embeddablePage/summaryData/{e.ExternalKey}");
        r.EnsureSuccessStatusCode();

        var dados = new Controllers.UpController.UploadData();
        if (r.Data["lastReceived"] != null)
        {
            dados.DataHoraDadosUTC = DateTime.UnixEpoch.AddMilliseconds((long?)r.Data["lastReceived"] ?? 0);

            string stacaoHoraKey = $"{e.Estacao}#{dados.DataHoraDadosUTC:yyyyMMddHHmm}";
            if (hsh.Contains(stacaoHoraKey))
            {
                logger.Information("[ExternalArchiver] Dados externos SKIP WL/{estacao}", e.Estacao);
                return;
            }
        }
        else return; // Não continua

        decimal? velocidadeVento = null;
        decimal? direcaoVento = null;
        decimal? radiacaoSolar = null;

        var current = r.Data["currConditionValues"];
        if (current != null) foreach (var v in current)
            {
                var name = (string?)v["sensorDataName"] ?? "";
                var convValue = ((string?)v["convertedValue"])?.Replace(",", ".");
                if (!decimal.TryParse(convValue, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal dVal)) continue;

                switch (name)
                {
                    case "Barometer": // Barometer
                        dados.PressaoAr = dVal;
                        break;
                    case "Temp": // Temp
                        dados.TemperaturaAr = dVal;
                        break;
                    case "Hum": // Hum
                        dados.UmidadeAr = dVal;
                        break;
                    case "Rain Rate": // Rain Rate
                        dados.Precipitacao = Math.Round(dVal / 60, 2); // h -> min
                        break;

                    case "Wind Speed":
                        velocidadeVento = dVal;
                        break;
                    case "Wind Direction":
                        direcaoVento = (decimal?)v["value"];
                        break;
                    case "Solar Rad":
                        radiacaoSolar = dVal;
                        break;
                }
            }
        var aggr = r.Data["aggregatedValues"];
        if (aggr != null) foreach (var v in aggr)
            {
                var name = (string?)v["sensorDataName"];
                if (name != "Rain") continue;

                string? convValue = null;
                var cv = v["convertedValues"];
                if (cv != null) convValue = ((string?)cv["MONTH"])?.Replace(",", ".");
                if (!decimal.TryParse(convValue, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal dVal)) continue;

                dados.PrecipitacaoTotal = dVal;
            }

        var obj = new
        {
            dados.DataHoraDadosUTC,
            dados.TemperaturaAr,
            dados.PressaoAr,
            dados.UmidadeAr,
            dados.Precipitacao,
            dados.PrecipitacaoTotal,
            velocidadeVento,
            direcaoVento,
            radiacaoSolar,
        };

        Controllers.UpController.sFinalizaGravacaoDados(db, logger, dados, Newtonsoft.Json.JsonConvert.SerializeObject(obj), $"EX.{e.Id}", e.Estacao);
        logger.Information("[ExternalArchiver] Dados externos WL/{estacao}", e.Estacao);
    }
    private async Task catalogarCemaden(DAO.DBModels.TBCatalogarExternas e)
    {
        var recentesEstacao = db.ListarDados(e.Estacao);
        var hsh = recentesEstacao.Select(o => $"{o.Estacao}#{o.DataHoraDadosUTC:yyyyMMddHHmm}").ToHashSet();

        string url = $"https://resources.cemaden.gov.br/graficos/interativo/grafico_pcds.php?idpcd={e.ExternalKey}&hr=24&dia=185";

        var r = await httpClient.GetAsync(url);
        if (!r.IsSuccessStatusCode) throw new HttpRequestException($"[{r.RequestMessage.Method}] {r.RequestMessage.RequestUri} [{r.StatusCode}] failed with {r.ReasonPhrase}");

        var html = await r.Content.ReadAsStringAsync();
        var blScript = html.Split("<script ");
        var graficos = blScript.Where(h => h.Contains("Grafico(")).ToArray();

        var js = graficos[0];
        var ixGrafico = js.IndexOf("{");
        js = js.Substring(ixGrafico);

        var ixFim = js.IndexOf("]);");
        js = js.Substring(0, ixFim);

        js = js.Replace("Grafico(", "\"data\":[");
        js = js + "]]}";
        js = js.Replace("'", "\"");

        var objData = JObject.Parse(js)["data"];
        var arrHorarios = objData[4].ToArray().Select(o => (string?)o ?? "").ToArray();
        var arrValores = objData[6].ToArray().Select(o => (decimal?)o ?? 0).ToArray();

        for (int i = 0; i < arrHorarios.Length; i++)
        {
            var dados = new Controllers.UpController.UploadData();

            var strDt = arrHorarios[i];
            strDt = strDt.Replace("h", ":")
                         .Replace("  ", " ")
                         .Replace(" UTC", ":00")
                         .Replace(" ", "T")
                         ;

            dados.DataHoraDadosUTC = DateTime.Parse(strDt + "z");
            dados.Precipitacao = arrValores[i];

            string stacaoHoraKey = $"{e.Estacao}#{dados.DataHoraDadosUTC:yyyyMMddHHmm}";
            if (hsh.Contains(stacaoHoraKey))
            {
                logger.Information("[ExternalArchiver] Dados externos SKIP CDM/{estacao}", e.Estacao);
                continue;
            }

            var serObj = new
            {
                dados.DataHoraDadosUTC,
                dados.Precipitacao
            };

            Controllers.UpController.sFinalizaGravacaoDados(db, logger, dados, Newtonsoft.Json.JsonConvert.SerializeObject(serObj), $"EX.{e.Id}", e.Estacao);
            logger.Information("[ExternalArchiver] Dados externos CDM/{estacao}", e.Estacao);
        }
    }

}
