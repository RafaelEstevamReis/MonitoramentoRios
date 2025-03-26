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
    public long UltimoEnvio { get; set; }
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
    public string? type { get; set; }

    // Dados Internos
    public DateTime DataHoraDadosUTC { get; set; }
    public decimal? ForcaSinal { get; set; }
    public decimal? TemperaturaInterna { get; set; }
    public decimal? TensaoBateria { get; set; }
    public decimal? PercentBateria { get; set; }
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
    public string? ImgPath { get; set; } = string.Empty;

    public string RawData { get; set; } = string.Empty;
    public string IP_Origem { get; set; } = string.Empty;
    public int Nonce { get; internal set; }
}
public class TBDadosEstacoesHora
{
    [PrimaryKey]
    public long Id { get; set; }
    [Index("ixTBDadosEstacoesHora_Estacao")]
    /// <summary>
    /// PubKey ou Hash da ApiKEY da estação
    /// </summary>
    public string Estacao { get; set; } = string.Empty;

    [Index("ixTBDadosEstacoesHora_HourKey")]
    public int HourKey { get; set; }

    // Dados Internos
    public DateTime DataHoraDadosUTC { get; set; }

    public int DataCount { get; set; }
    public long FirstDataRow { get; set; }
    public long LastDataRow { get; set; }

    public decimal? ForcaSinal_MAX { get; set; }
    public decimal? ForcaSinal_MIN { get; set; }
    public decimal? ForcaSinal_AVG { get; set; }
    public decimal? ForcaSinal_StdDev { get; set; }

    public decimal? TemperaturaInterna_MAX { get; set; }
    public decimal? TemperaturaInterna_MIN { get; set; }
    public decimal? TemperaturaInterna_AVG { get; set; }
    public decimal? TemperaturaInterna_StdDev { get; set; }

    public decimal? TensaoBateria_MAX { get; set; }
    public decimal? TensaoBateria_MIN { get; set; }
    public decimal? TensaoBateria_AVG { get; set; }
    public decimal? TensaoBateria_StdDev { get; set; }
    public decimal? TensaoBateria_Trend { get; set; }

    public decimal? PercentBateria_MAX { get; set; }
    public decimal? PercentBateria_MIN { get; set; }
    public decimal? PercentBateria_AVG { get; set; }
    public decimal? PercentBateria_StdDev { get; set; }
    public decimal? PercentBateria_Trend { get; set; }

    // Medições
    public decimal? TemperaturaAr_MAX { get; set; }
    public decimal? TemperaturaAr_MIN { get; set; }
    public decimal? TemperaturaAr_AVG { get; set; }
    public decimal? TemperaturaAr_StdDev { get; set; }
    public decimal? TemperaturaAr_Trend { get; set; }

    public decimal? UmidadeAr_MAX { get; set; }
    public decimal? UmidadeAr_MIN { get; set; }
    public decimal? UmidadeAr_AVG { get; set; }
    public decimal? UmidadeAr_StdDev { get; set; }
    public decimal? UmidadeAr_Trend { get; set; }

    public decimal? PressaoAr_MAX { get; set; }
    public decimal? PressaoAr_MIN { get; set; }
    public decimal? PressaoAr_AVG { get; set; }
    public decimal? PressaoAr_StdDev { get; set; }
    public decimal? PressaoAr_Trend { get; set; }

    public decimal? Precipitacao_MAX { get; set; }
    public decimal? Precipitacao_MIN { get; set; }
    public decimal? Precipitacao_AVG { get; set; }
    public decimal? Precipitacao_StdDev { get; set; }
    public decimal? Precipitacao_Trend { get; set; }

    public decimal? NivelRio_MAX { get; set; }
    public decimal? NivelRio_MIN{ get; set; }
    public decimal? NivelRio_AVG { get; set; }
    public decimal? NivelRio_StdDev { get; set; }
    public decimal? NivelRio_Trend { get; set; }

}
