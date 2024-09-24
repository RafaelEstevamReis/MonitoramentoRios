namespace Web.Data.DAO.DBModels;

using Simple.DatabaseWrapper.Attributes;
using System;

public class TBEstacoes
{
    [PrimaryKey]
    public long Id { get; set; }

    [Unique()]
    /// <summary>
    /// PubKey ou Hash da ApiKEY da estação
    /// </summary>
    public string Estacao { get; set; } = string.Empty;
    public string ApiKEY { get; set; } = string.Empty;
    public string NomeResponsavel { get; set; } = string.Empty;
    public string NomeEstacao { get; set; } = string.Empty;
}

public class TBDadosEstacoes
{
    [PrimaryKey]
    public long Id { get; set; }
    public DateTime RecebidoUTC { get; set; }
    [Index("ixTBDadosEstacoes_Estacao")]
    /// <summary>
    /// PubKey ou Hash da ApiKEY da estação
    /// </summary>
    public string Estacao { get; set; } = string.Empty;

    // Dados Internos
    public DateTime DataHoraDadosUTC { get; set; }
    public decimal? ForcaSinal { get; set; }
    public decimal? TemperaturaInterna { get; set; }
    public decimal? TensaoBateria { get; set; }
    /// <summary>
    /// Data que os dados foram gerados segundo a placa
    /// </summary>
    // Medições
    public decimal? TemperaturaAr { get; set; }
    public decimal? UmidadeAr { get; set; }
    public decimal? PressaoAr { get; set; }
    public decimal? Precipitacao { get; set; } // Atual em mm/min
    /// <summary>
    /// Medição calibrada
    /// </summary>
    public decimal? NivelRio { get; set; }
    /// <summary>
    ///  Medição direta sem calibração ou ajuste
    /// </summary>
    public decimal? NivelRio_RAW { get; set; }

    public string RawData { get; set; } = string.Empty;
    public string IP_Origem { get; set; } = string.Empty;
}
