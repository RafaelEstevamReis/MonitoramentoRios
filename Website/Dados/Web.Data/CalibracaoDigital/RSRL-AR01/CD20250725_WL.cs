namespace Web.Data.CalibracaoDigital.RSRL_AR01;

using System;
using System.Text.Json.Nodes;
using Web.Data.Controllers;

public class CD20250725_WL : ICalibracaoDigital
{
    public string Estacao => "CF98FCFA7E9EE7C1";

    public DateTime ValidadeInicioUTC => new DateTime(2025, 07, 25, 0, 0, 0, DateTimeKind.Utc);
    public DateTime ValidadeFimUTC => new DateTime(2025, 08, 13, 0, 0, 0, DateTimeKind.Utc);

    public bool ProcessaDados(UpController.UploadData d, JsonNode? r)
    {
        if (!d.NivelRio_RAW.HasValue) return false;

        if(d.NivelRio_RAW > 5.4M)
        {
            d.NivelRio = null; // Além do alcance do sensor
            return true;
        }

        // Pegou na Raíz
        if (d.NivelRio_RAW > 1.5M && d.NivelRio_RAW < 2.1M)
        {
            d.NivelRio = null; // Pegou a raíz
            return true;
        }

        return false; // Não mexeu
    }
}
