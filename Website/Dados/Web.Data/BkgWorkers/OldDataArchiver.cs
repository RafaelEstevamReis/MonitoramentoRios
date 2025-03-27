namespace Web.Data.BkgWorkers;

using Microsoft.Extensions.Hosting;
using System;
using System.Threading.Tasks;
using System.Threading;
using Serilog;
using Web.Data.DAO;
using System.Linq;
using Simple.Sqlite;

public class OldDataArchiver : IHostedService, IDisposable
{
    private readonly ILogger logger;
    private readonly DB db;
    private Timer _timer;
    public OldDataArchiver(DB db, ILogger logger)
    {
        this.db = db;
        this.logger = logger;
    }
    public void Dispose()
    {
        _timer?.Dispose();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.Information("[OldDataArchiver] Iniciando serviço de limpeza de dados...");
        _timer = new Timer(executaVerificacaoAsync, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20));

        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    private void executaVerificacaoAsync(object state)
    {
        try
        {
            // Task 1 - Remover dados brutos +3Mo
            removerLinhas90d();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[OldDataArchiver] Error {msg}", ex.Message);
        }
    }

    private void removerLinhas90d()
    {
        var antigos = db.ListarAntigos(limit: 5000)
                        .Where(o => (DateTime.UtcNow - o.RecebidoUTC) .TotalDays > 90)
                        .ToArray();

        if (antigos.Length == 0)
        {
            logger.Information("[OldDataArchiver] NoData");
            return;
        }
        logger.Information("[OldDataArchiver] {rows} to Archive | Older: {old} Newer: {new}", antigos.Length, antigos[0].DataHoraDadosUTC, antigos[^1].DataHoraDadosUTC);

        var gmes = antigos.GroupBy(o => o.RecebidoUTC.ToString("yyyyMM"))
                          .ToArray();

        foreach(var grupoMes in gmes)
        {
            var itens = grupoMes.ToArray();
            using var cnn = ConnectionFactory.CreateConnection($"data/dbOld_{grupoMes.Key}.db");
            cnn.CreateTables()
               .Add<DAO.DBModels.TBDadosEstacoes>()
               .Commit();

            cnn.BulkInsert(itens, OnConflict.Replace);
            logger.Information("[OldDataArchiver] Archiving {rows} from {date}", itens.Length, grupoMes.Key);

            db.RemoveAntigos(itens.Select(o => o.Id));
        }

    }
}
