using System;
using System.Text.Json.Nodes;
using Web.Data.Controllers;

namespace Web.Data.CalibracaoDigital;

public interface ICalibracaoDigital
{
    public string Estacao { get; }

    DateTime ValidadeInicioUTC { get; }
    DateTime ValidadeFimUTC { get; }

    bool ProcessaDados(UpController.UploadData d, JsonNode? r);
}
