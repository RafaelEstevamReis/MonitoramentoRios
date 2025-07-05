using System;
using System.Text.Json.Nodes;
using Web.Data.Controllers;

namespace Web.Data.CalibracaoDigital.RLRS_GLLS;

public class CD20250627_vBat : ICalibracaoDigital
{
    public string Estacao => "9A6EE7B45495BB7F";

    public DateTime ValidadeInicioUTC => new DateTime(2025, 06, 27, 0, 0, 0, DateTimeKind.Utc);
    public DateTime ValidadeFimUTC => new DateTime(2025, 07, 04, 0, 0, 0, DateTimeKind.Utc); // Em 04/07 foi corrigido o envio

    public bool ProcessaDados(UpController.UploadData d, JsonNode? r)
    {
        // Tem ACRaw?
        var adcRaw = r["AdcRaw"];
        if (adcRaw == null) return false;

        decimal? adc = (decimal?)adcRaw;
        if (!adc.HasValue) return false;

        // Medição 2025-06-27T15:06
        //  0.2200v => 4.12v
        var ratio = 4.12M / 0.22M;
        var vBat = Math.Round(adc.Value * ratio, 2);

        // Margem?
        if (d.TensaoBateria != null)
        {
            var diff = Math.Abs(vBat - d.TensaoBateria.Value);
            var diffP = diff / vBat;
            if (diffP < 0.1M) return false; // <10%
        }

        d.TensaoBateria = vBat;
        // Ajusta percentual
        d.PercentBateria = Math.Round((decimal)vBatToPercent((float)vBat), 2);

        return true;

    }
    private static float vBatToPercent(float vBat)
    {
        if (vBat >= 4.2f)
        { // 100%
            return 100.0f;
        }
        else if (vBat < 2.5f)
        { // 0%
            return 0.0f;
        }
        else if (vBat > 4.05f)
        { // 90%
            float x = vBat;
            return ((x - 4.05f) / (4.2f - 4.05f)) * (100.0f - 90.0f) + 90.0f;
        }
        else if (vBat > 3.45f)
        { // 20%~90%
            float x = vBat;
            return ((x - 3.45f) / (4.05f - 3.45f)) * (90.0f - 20.0f) + 20.0f;
        }
        else
        {
            float x = vBat;
            return 5.0f;
        }
    }
}
