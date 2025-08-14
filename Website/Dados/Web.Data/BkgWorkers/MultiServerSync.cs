namespace Web.Data.BkgWorkers;

using Microsoft.Extensions.Hosting;
using Serilog;
using Simple.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Web.Data.DAO;

public class MultiServerSync : IHostedService, IDisposable
{
    private readonly ILogger logger;
    private readonly DB db;
    private readonly string key;
    private readonly ClientInfo client;

    private Timer _timer;
    public MultiServerSync(ILogger? logger, DB db, string host, string key)
    {
        this.db = db;
        this.key = key;
        this.logger = logger;

        if (!string.IsNullOrEmpty(host)) client = new ClientInfo(host);
    }
    public void Dispose()
    {
        _timer?.Dispose();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (client == null)
        {
            logger.Information("[MultiServerSync] Disabled...");
            return Task.CompletedTask;
        }

        TimeSpan start;
#if DEBUG
        start = TimeSpan.FromSeconds(12);
#else
        start = TimeSpan.FromMinutes(30);
#endif

        logger.Information("[MultiServerSync] Iniciando serviço sincronia...");
        _timer = new Timer(executaVerificacaoAsync, null, start, TimeSpan.FromMinutes(12));

        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    private async void executaVerificacaoAsync(object? state)
    {
        logger.Information("[MultiServerSync] Listando estações");
        // Listar todas as estações
        var rEstacoes = await client.GetAsync<Controllers.EstacoesController.DadosEstacao[]>("/estacoes").GetSuccessfulData();
        sincronizaEstacoes(rEstacoes);

        // Pedir histórico
        foreach (var remota in rEstacoes)
        {
            if (remota.UltimoEnvio == null) continue; // Não tem
            var ultimoAge = DateTime.UtcNow - remota.UltimoEnvio.DataHoraDadosUTC;
            if (ultimoAge.TotalHours > 24) continue; // Muito velho

            // Solicita últimos
            logger.Information("[MultiServerSync] Solicitando dados para: {e}/{n}", remota.Estacao, remota.NomeEstacao);
            var eventos = await client.GetAsync<Controllers.EstacoesController.DadosColetados[]>("/estacoes/dados", new
            {
                estacao = remota.Estacao,
                limit = 64,
            }).GetSuccessfulData();

            sincronizaEventos(remota, eventos);
        }
    }

    private void sincronizaEstacoes(Controllers.EstacoesController.DadosEstacao[] dados)
    {
        List<string> lstPedirDados = [];
        var estacoesDB = db.ListarEstacoes();
        // Compara
        foreach (var d in dados)
        {
            var edb = estacoesDB.FirstOrDefault(o => o.Estacao == d.Estacao);
            if (edb != null)
            {
                // Atualizar?
            }
            else
            {
                // Não tem
                lstPedirDados.Add(d.Estacao);
            }
        }

        // Pedir detalhes das que não tem
        if (string.IsNullOrEmpty(key))
        {
            logger.Warning("[MultiServerSync] NO-KEY Não é possível obter dados de estações faltantes: {e}", string.Join(',', lstPedirDados));
            return;
        }
    }
    private void sincronizaEventos(Controllers.EstacoesController.DadosEstacao remota, Controllers.EstacoesController.DadosColetados[] eventosRemoto)
    {
        TimeSpan janelaDeTempo = TimeSpan.FromMinutes(5);
        List<Controllers.EstacoesController.DadosColetados> eventosParaAdicionar = [];

        var eventosDB = db.ListarDados(remota.Estacao, 128); // Eventos no servidor local

        foreach (var eventoRemoto in eventosRemoto.Where(o => o.Nonce > 0))
        {
            // Verifica se o evento remoto já existe no banco de dados
            bool isDuplicado = eventosDB.Any(eventoLocal =>
                eventoLocal.Estacao == remota.Estacao &&
                // Verifica se o Nonce é igual
                eventoLocal.Nonce == eventoRemoto.Nonce &&
                // Verifica se a diferença de tempo está dentro da janela de tempo
                Math.Abs((eventoLocal.RecebidoUTC - eventoRemoto.RecebidoUTC).TotalMinutes) <= janelaDeTempo.TotalMinutes
            );

            // Se não for duplicado, adiciona à lista de eventos para inserir
            if (!isDuplicado)
            {
                eventosParaAdicionar.Add(eventoRemoto);
            }
        }

        if (eventosParaAdicionar.Count > 0)
        {
            // Adiciona os eventos não duplicados ao banco de dados
            foreach (var evento in eventosParaAdicionar)
            {
                //db.InserirDados(evento); // Método fictício para inserir no banco
                db.Registra(new DAO.DBModels.TBDadosEstacoes
                {
                    Id = 0, // NOVO
                    RecebidoUTC = evento.RecebidoUTC,
                    Estacao = remota.Estacao,
                    type = evento.type,
                    DataHoraDadosUTC = evento.DataHoraDadosUTC,
                    ForcaSinal = evento.ForcaSinal,
                    TemperaturaInterna = evento.TemperaturaInterna,
                    TensaoBateria = evento.TensaoBateria,
                    PercentBateria = evento.PercentBateria,
                    TemperaturaAr = evento.TemperaturaAr,
                    UmidadeAr = evento.UmidadeAr,
                    PressaoAr = evento.PressaoAr,
                    Precipitacao = evento.Precipitacao,
                    Precipitacao10min = evento.Precipitacao10min,
                    PrecipitacaoTotal = evento.PrecipitacaoTotal,
                    NivelRio = evento.NivelRio,
                    NivelRio_RAW = evento.NivelRio_RAW,
                    //RawData = evento.RawData,
                    //IP_Origem
                    Nonce = evento.Nonce,
                    Source = DAO.DBModels.TBDadosEstacoes.DataSource.Sync,
                });
            }
            logger.Information("[MultiServerSync] {e}/{n} Novos Registros: {qtd}", remota.Estacao, remota.NomeEstacao, eventosParaAdicionar.Count);
        }
    }

}
