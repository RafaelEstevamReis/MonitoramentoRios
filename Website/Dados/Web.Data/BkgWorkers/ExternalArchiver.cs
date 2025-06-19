namespace Web.Data.BkgWorkers;

using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using Serilog;
using Simple.API;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Web.Data.DAO;

public class ExternalArchiver : IHostedService, IDisposable
{
    private readonly ILogger logger;
    private readonly DB db;
    private readonly ClientInfo wlClient;

    private Timer _timer;
    public ExternalArchiver(DB db, ILogger logger)
    {
        this.db = db;
        this.logger = logger;

        wlClient = new ClientInfo("https://www.weatherlink.com");
        wlClient.SetHeader("x-requested-with", "XMLHttpRequest");
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
                if (e.Origem == DAO.DBModels.TBCatalogarExternas.DataSource.WLink)
                {
                    await catalogarWLink(e);
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
        var r = await wlClient.GetAsync<JObject>($"/embeddablePage/summaryData/{e.ExternalKey}");
        r.EnsureSuccessStatusCode();

        var dados = new Controllers.UpController.UploadData();
        if (r.Data["lastReceived"] != null)
        {
            dados.DataHoraDadosUTC = DateTime.UnixEpoch.AddMilliseconds((long?)r.Data["lastReceived"] ?? 0);
        }        

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

        Controllers.UpController.sFinalizaGravacaoDados(db, logger, dados, Newtonsoft.Json.JsonConvert.SerializeObject(dados), $"EX.{e.Id}", e.Estacao);
        logger.Information("[ExternalArchiver] Dados externos {estacao}", e.Estacao);
    }
}
