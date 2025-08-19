using System;
using System.Text.Json.Nodes;
using Web.Data.Controllers;

namespace Web.Data.CalibracaoDigital.RSRL_CH01;

public class CD20250819_WL : ICalibracaoDigital
{
    public string Estacao => "48B1162D47EC0FE6";

    public DateTime ValidadeInicioUTC => new DateTime(2025, 08, 19, 0, 0, 0, DateTimeKind.Utc);
    public DateTime ValidadeFimUTC => new DateTime(2025, 10, 19, 0, 0, 0, DateTimeKind.Utc);

    public bool ProcessaDados(UpController.UploadData d, JsonNode? r)
    {
        if (!d.NivelRio_RAW.HasValue) return false; // Não mexe

        if (d.NivelRio_RAW > 7.5M)
        {
            d.NivelRio = null; // Além do alcance do sensor
            return true;
        }
        if (d.NivelRio_RAW < 0.5M)
        {
            d.NivelRio = null; // Água não chega
            return true;
        }

        if (d.NivelRio_RAW > 3.0M)
        {
            d.NivelRio = null; // Ficaria negativo
            return true;
        }

        d.NivelRio = 3.0M - d.NivelRio_RAW;
        return true;
    }
}
