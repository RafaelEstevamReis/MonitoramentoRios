using System;
using System.Text.Json.Nodes;
using Web.Data.Controllers;

namespace Web.Data.CalibracaoDigital.RSRL_CE02;

public class CD20251210_WL : ICalibracaoDigital
{
    public string Estacao => "AAA7CCD686C44D8E";

    public DateTime ValidadeInicioUTC => new DateTime(2025, 12, 10, 0, 0, 0, DateTimeKind.Utc);
    public DateTime ValidadeFimUTC => new DateTime(2026, 02, 10, 0, 0, 0, DateTimeKind.Utc);

    public bool ProcessaDados(UpController.UploadData d, JsonNode? r)
    {
        if (!d.NivelRio_RAW.HasValue) return false; // Não mexe

        // Sensor fica a 5mde altura
        if (d.NivelRio_RAW > 7.5M)
        {
            d.NivelRio = null; // Além do alcance do sensor
            return true;
        }
        if (d.NivelRio_RAW < 1.5M)
        {
            d.NivelRio = null; // Água não chega
            return true;
        }

        return false; // Não mexe
    }

}
