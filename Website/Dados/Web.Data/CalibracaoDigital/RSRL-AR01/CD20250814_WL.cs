namespace Web.Data.CalibracaoDigital.RSRL_AR01;

using System;
using System.Text.Json.Nodes;
using Web.Data.Controllers;

public class CD20250814_WL : ICalibracaoDigital
{
    public string Estacao => "CF98FCFA7E9EE7C1";

    public DateTime ValidadeInicioUTC => new DateTime(2025, 08, 14, 0, 0, 0, DateTimeKind.Utc);
    public DateTime ValidadeFimUTC => new DateTime(2025, 10, 14, 0, 0, 0, DateTimeKind.Utc); // Em 04/07 foi corrigido o envio

    public bool ProcessaDados(UpController.UploadData d, JsonNode? r)
    {
        if (!d.NivelRio_RAW.HasValue) return false;

        if (d.NivelRio_RAW > 5.5M)
        {
            d.NivelRio = null; // Além do alcance do sensor
            return true;
        }
        if (d.NivelRio_RAW < 1.5M)
        {
            d.NivelRio = null; // Falso positivo 
            return true;
        }

        // Pegou no chão
        if (d.NivelRio_RAW > 2.0M && d.NivelRio_RAW < 2.9M)
        {
            d.NivelRio = null; // chão
            return true;
        }

        //d.NivelRio = d.NivelRio_RAW - 4.5M;
        d.NivelRio = 5.5M - d.NivelRio_RAW;

        return true;
    }
}
