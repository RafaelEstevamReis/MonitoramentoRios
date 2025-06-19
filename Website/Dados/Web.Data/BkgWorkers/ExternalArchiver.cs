namespace Web.Data.BkgWorkers;

using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using Serilog;
using Simple.API;
using System;
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
        logger.Information("[ExternalArchiver] Iniciando serviço de Archive Externo de dados...");
        _timer = new Timer(executaVerificacaoAsync, null, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(10));

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

        var dados = new Controllers.UpController.UploadData()
        {
            DataHoraDadosUTC = DateTime.UnixEpoch.AddMilliseconds((long)r.Data["lastReceived"]),
        };

        var current = r.Data["currConditionValues"];
        foreach (var v in current)
        {
            var id = (int)v["sensorDataTypeId"];
            //var name = (string)v["sensorDataName"];
            //var value = (decimal)v["value"];

            switch (id)
            {
                case 50: // Barometer
                    dados.PressaoAr = (decimal)v["convertedValue"];
                    break;
                case 58: // Temp
                    dados.TemperaturaAr = (decimal)v["convertedValue"];
                    break;
                case 59: // Hum
                    dados.UmidadeAr = (decimal)v["convertedValue"];
                    break;
                case 63: // Rain Rate
                    dados.Precipitacao = Math.Round((decimal)v["convertedValue"] / 60, 2); // h -> min
                    break;
            }
        }
        var aggr = r.Data["aggregatedValues"];
        foreach (var v in aggr)
        {
            var name = (string)v["sensorDataName"];
            if (name != "Rain") continue;

            var month = (decimal)v["convertedValues"]["MONTH"];
            dados.PrecipitacaoTotal = month;
        }

        Controllers.UpController.sFinalizaGravacaoDados(db, logger, dados, Newtonsoft.Json.JsonConvert.SerializeObject(dados), $"EX.{e.Id}", e.Estacao);
        logger.Information("[ExternalArchiver] Dados externos {estacao}", e.Estacao);
    }
}
