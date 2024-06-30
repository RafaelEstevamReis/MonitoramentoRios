namespace Web.Data.DAO;

using Simple.Sqlite;
using System.Collections.Generic;

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
        cnn.CreateTables()
           .Add<DBModels.TBEstacoes>()
           .Add<DBModels.TBDadosEstacoes>()
           .Commit();

        var allKeys = cnn.Query<string>($"SELECT {nameof(DBModels.TBEstacoes.ApiKEY)} FROM {nameof(DBModels.TBEstacoes)}");
        foreach (var k in allKeys) apiKeys.Add(k);

    }

    public IEnumerable<DBModels.TBDadosEstacoes> ListarEstacoes(string? estacao = null, int limit = 50)
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

    public bool IsValidKey(string key) => apiKeys.Contains(key);

    public void Registra(DBModels.TBDadosEstacoes d)
    {
        using var cnn = db.GetConnection();
        cnn.Insert(d);
    }
    public void NovaEstacao(DBModels.TBEstacoes estacao)
    {
        using var cnn = db.GetConnection();
        cnn.Insert(estacao, OnConflict.Abort);

        apiKeys.Add(estacao.ApiKEY);
    }
}
