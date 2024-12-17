﻿namespace Web.Data.DAO;

using Simple.DatabaseWrapper.Interfaces;
using Simple.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;

public class DB
{
    private ConnectionFactory db;
    HashSet<string> apiKeys = [];

    public DB(string path)
    {
        db = ConnectionFactory.FromFile(path);
    }

    public void Setup()
    {
        using var cnn = db.GetConnection();
        var result = cnn.CreateTables()
           .Add<DBModels.TBEstacoes>()
           .Add<DBModels.TBDadosEstacoes>()
           .Add<DBModels.TBDadosEstacoesHora>()
           .Commit();

        if (result.Length > 0) // Teve migrations
        {
            executaMigrations(cnn, result);
        }

        //cnn.Execute($"DELETE FROM {nameof(DBModels.TBDadosEstacoesHora)} WHERE NivelRio_AVG is NULL");

        var allKeys = cnn.Query<string>($"SELECT {nameof(DBModels.TBEstacoes.ApiKEY)} FROM {nameof(DBModels.TBEstacoes)}");
        foreach (var k in allKeys) apiKeys.Add(k);
    }

    private static void executaMigrations(ISqliteConnection cnn, ITableCommitResult[] result)
    {
        var migEstacoes = result.FirstOrDefault(o => o.TableName == nameof(DBModels.TBEstacoes));
        if (migEstacoes != null && migEstacoes.ColumnsAdded.Length > 0)
        {
            // 20241217 - Adiciona uLtimos envios
            if (migEstacoes.ColumnsAdded.Any(c => c == "UltimoEnvio")) // Criou a últimos envios
            {
                var reg = cnn.Query<DBModels.TBEstacoes>("SELECT Estacao, MAX(Id) as Id FROM TBDadosEstacoes GROUP BY Estacao");

                foreach (var estacao in reg)
                {
                    cnn.Execute($"UPDATE {nameof(DBModels.TBEstacoes)} SET {nameof(DBModels.TBEstacoes.UltimoEnvio)} = @id WHERE {nameof(DBModels.TBEstacoes.Estacao)} = @estacao ", new
                    {
                        id = estacao.Id,
                        estacao = estacao.Estacao,
                    });
                }

            }
        }
    }

    public IEnumerable<DBModels.TBEstacoes> ListarEstacoes()
    {
        using var cnn = db.GetConnection();
        var estacoes = cnn.GetAll<DBModels.TBEstacoes>();
        foreach (var e in estacoes)
        {
            e.ApiKEY = "";
        }
        return estacoes;
    }
    public IEnumerable<DBModels.TBDadosEstacoes> ListarDados(string? estacao = null, int limit = 50)
    {
        if (limit > 512) limit = 512;

        using var cnn = db.GetConnection();

        if (estacao == null)
        {
            return cnn.Query<DBModels.TBDadosEstacoes>($"SELECT * FROM {nameof(DBModels.TBDadosEstacoes)} ORDER BY Id DESC LIMIT 0,{limit} ");
        }
        else
        {
            return cnn.Query<DBModels.TBDadosEstacoes>($"SELECT * FROM {nameof(DBModels.TBDadosEstacoes)} WHERE Estacao = @estacao ORDER BY Id DESC LIMIT 0,{limit} ", new { estacao });
        }
    }
    public DBModels.TBDadosEstacoes? ObterRegistroEstacao(long id)
    {
        if (id == 0) return null;
        using var cnn = db.GetConnection();
        return cnn.Get<DBModels.TBDadosEstacoes>(id);
    }

    public DBModels.TBDadosEstacoesHora? AgregadoHora(string estacao, int hourSpan)
    {
        if (string.IsNullOrEmpty(estacao)) return null;

        using var cnn = db.GetConnection();
        var qhora = cnn.Query<DBModels.TBDadosEstacoesHora>($"SELECT * FROM {nameof(DBModels.TBDadosEstacoesHora)} WHERE Estacao = @estacao AND HourKey = @hourSpan ", new
        {
            estacao,
            hourSpan,
        });

        var hora = qhora.FirstOrDefault();
        if (hora != null) return hora;

        // Coletar a hora manualmente 
        var inicio = DateTime.UnixEpoch.AddHours(hourSpan);
        var fim = DateTime.UnixEpoch.AddHours(hourSpan + 1).AddMilliseconds(-1);
        var qData = cnn.Query<DBModels.TBDadosEstacoes>($"SELECT * FROM {nameof(DBModels.TBDadosEstacoes)} WHERE Estacao = @estacao AND DataHoraDadosUTC BETWEEN @inicio AND @fim ", new
        {
            estacao,
            inicio,
            fim,
        }).ToArray();

        if (qData.Length == 0) return null; // Salvar que não tem? Dá full table scan

        var forcaSinal = DataAggregator.Aggregate(qData, o => o.ForcaSinal);
        var temperaturaInterna = DataAggregator.Aggregate(qData, o => o.TemperaturaInterna);
        var tensaoBateria = DataAggregator.Aggregate(qData, o => o.TensaoBateria);
        var percentBateria = DataAggregator.Aggregate(qData, o => o.PercentBateria);
        var temperaturaAr = DataAggregator.Aggregate(qData, o => o.TemperaturaAr);
        var umidadeAr = DataAggregator.Aggregate(qData, o => o.UmidadeAr);
        var pressaoAr = DataAggregator.Aggregate(qData, o => o.PressaoAr);
        var precipitacao = DataAggregator.Aggregate(qData, o => o.Precipitacao);
        var nivelRio = agregadorFiltrado(qData, o => o.NivelRio ?? o.NivelRio_RAW);

        hora = new DBModels.TBDadosEstacoesHora
        {
            Id = 0,
            Estacao = estacao,
            HourKey = hourSpan,
            DataHoraDadosUTC = inicio,
            DataCount = qData.Length,
            FirstDataRow = qData.Min(o => o.Id),
            LastDataRow = qData.Max(o => o.Id),

            // Dados de Força do Sinal
            ForcaSinal_MAX = forcaSinal.Max,
            ForcaSinal_MIN = forcaSinal.Min,
            ForcaSinal_AVG = forcaSinal.Avg,
            ForcaSinal_StdDev = forcaSinal.StdDev,

            // Dados de Temperatura Interna
            TemperaturaInterna_MAX = temperaturaInterna.Max,
            TemperaturaInterna_MIN = temperaturaInterna.Min,
            TemperaturaInterna_AVG = temperaturaInterna.Avg,
            TemperaturaInterna_StdDev = temperaturaInterna.StdDev,

            // Dados de Tensão da Bateria
            TensaoBateria_MAX = tensaoBateria.Max,
            TensaoBateria_MIN = tensaoBateria.Min,
            TensaoBateria_AVG = tensaoBateria.Avg,
            TensaoBateria_StdDev = tensaoBateria.StdDev,
            TensaoBateria_Trend = tensaoBateria.Trend,

            // Dados de Percentual da Bateria
            PercentBateria_MAX = percentBateria.Max,
            PercentBateria_MIN = percentBateria.Min,
            PercentBateria_AVG = percentBateria.Avg,
            PercentBateria_StdDev = percentBateria.StdDev,
            PercentBateria_Trend = percentBateria.Trend,

            // Dados de Temperatura do Ar
            TemperaturaAr_MAX = temperaturaAr.Max,
            TemperaturaAr_MIN = temperaturaAr.Min,
            TemperaturaAr_AVG = temperaturaAr.Avg,
            TemperaturaAr_StdDev = temperaturaAr.StdDev,
            TemperaturaAr_Trend = temperaturaAr.Trend,

            // Dados de Umidade do Ar
            UmidadeAr_MAX = umidadeAr.Max,
            UmidadeAr_MIN = umidadeAr.Min,
            UmidadeAr_AVG = umidadeAr.Avg,
            UmidadeAr_StdDev = umidadeAr.StdDev,
            UmidadeAr_Trend = umidadeAr.Trend,

            // Dados de Pressão do Ar
            PressaoAr_MAX = pressaoAr.Max,
            PressaoAr_MIN = pressaoAr.Min,
            PressaoAr_AVG = pressaoAr.Avg,
            PressaoAr_StdDev = pressaoAr.StdDev,
            PressaoAr_Trend = pressaoAr.Trend,

            // Dados de Precipitação
            Precipitacao_MAX = precipitacao.Max,
            Precipitacao_MIN = precipitacao.Min,
            Precipitacao_AVG = precipitacao.Avg,
            Precipitacao_StdDev = precipitacao.StdDev,
            Precipitacao_Trend = precipitacao.Trend,

            // Dados do Nível do Rio
            NivelRio_MAX = nivelRio.Max,
            NivelRio_MIN = nivelRio.Min,
            NivelRio_AVG = nivelRio.Avg,
            NivelRio_StdDev = nivelRio.StdDev,
            NivelRio_Trend = nivelRio.Trend,
        };

        // Salva db, exceto se for hora corrente
        var horaAgora = (int)(DateTime.UtcNow - DateTime.UnixEpoch).TotalHours;
        if (horaAgora != hourSpan) cnn.Insert(hora, OnConflict.Replace);

        return hora;
    }

    private DataAggregator.Result agregadorFiltrado(DBModels.TBDadosEstacoes[] qData, Func<DBModels.TBDadosEstacoes, decimal?> selector)
    {
        List<decimal?> valores = qData.Select(selector).Where(o => o.HasValue).ToList();
        // retira o pior menor e pior maior
        if (valores.Count > 3)
        {
            valores = DataAggregator.TruncarValores(valores, trimSize: 1).ToList();
        }

        // Calcula o desvio
        var agr1 = DataAggregator.Aggregate(valores);
        if (agr1.Count == 1) return agr1;

        // Corta todos 1 desvio fora
        var valores2 = agr1.Values.Where(o => Math.Abs(o - agr1.Avg ?? 0) <= agr1.StdDev).ToList();

        var agr2 = DataAggregator.Aggregate(valores2);

        return agr2;
    }

    public bool IsValidKey(string key) => apiKeys.Contains(key);

    public long Registra(DBModels.TBDadosEstacoes d)
    {
        using var cnn = db.GetConnection();
        var id = cnn.Insert(d);

        cnn.Execute($"UPDATE {nameof(DBModels.TBEstacoes)} SET {nameof(DBModels.TBEstacoes.UltimoEnvio)} = @id WHERE {nameof(DBModels.TBEstacoes.Estacao)} = @estacao ", new
        {
            id,
            estacao = d.Estacao,
        });

        return id;
    }
    public void NovaEstacao(DBModels.TBEstacoes estacao)
    {
        using var cnn = db.GetConnection();
        cnn.Insert(estacao, OnConflict.Abort);

        apiKeys.Add(estacao.ApiKEY);
    }

}
