namespace Web.Data.BkgWorkers;

using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Web.Data.DAO;
using Web.Data.Services;

public class WeatherOpenMeteo(DB db, ILogger logger, OpenMeteoService openMeteoService) : IHostedService, IDisposable
{
    const int HOURS_SECO = 12;
    const int HOURS_CHUVA = 4;

    static readonly LocalidadeBusca[] Localizacoes = [
        new ("RSRL",  -29.64, -50.57),
    ];

    private readonly ILogger logger = logger;
    private readonly OpenMeteoService openMeteoService = openMeteoService;
    private readonly DB db = db;
    private Timer _timer;

    public void Dispose()
    {
        _timer?.Dispose();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        TimeSpan tsPrimeira = TimeSpan.FromMinutes(15);
#if DEBUG
        tsPrimeira = TimeSpan.FromSeconds(15);
#endif

        logger.Information("[WeatherOpenMeteo] Iniciando serviço de meteorologia OpenMeteo...");
        _timer = new Timer(executaVerificacaoAsync,
                           null,
                           tsPrimeira,
                           TimeSpan.FromHours(2) // Executa a cada duas horas, mas ignora se os dados forem recentes
                           );

        await Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    private async void executaVerificacaoAsync(object? state)
    {
        foreach (var local in Localizacoes)
        {
            // Chega última
            if (temRecente(local.Code, out TimeSpan recenteAge, out bool recenteTemChuva))
            {
                logger.Information("[WeatherOpenMeteo] {regiao} Tem recente, SKIP | Hours: {h:N1} | Chuva: {bChuva}", local.Code, recenteAge.TotalHours, recenteTemChuva);
                return;
            }
            logger.Information("[WeatherOpenMeteo] {regiao} Atualiza Dados | Hours: {h:N1} | Chuva: {bChuva}", local.Code, recenteAge.TotalHours, recenteTemChuva);

            try
            {
                var previsao = await openMeteoService.ObterPrevisaoLocalidade(local);
                var dhColeta = DateTime.UtcNow;

                var d1h = previsao.hourly;
                var lst = new List<DAO.DBModels.TBWeather>();
                for (int i = 0; i < d1h.time.Length; i++)
                {
                    lst.Add(new DAO.DBModels.TBWeather
                    {
                        Id = 0,
                        ColetaUTC = dhColeta, // Mesma data para todos os registros
                        Lat = Convert.ToDecimal(previsao.latitude),
                        Lon = Convert.ToDecimal(previsao.longitude),

                        ForecastUTC = d1h.time[i],
                        LuzDia = d1h.is_day[i] > 0,
                        UvIndex = d1h.uv_index[i],
                        Temperatura = d1h.temperature_2m[i],
                        SensacaoTermica = d1h.apparent_temperature[i],
                        Umidade = d1h.relative_humidity_2m[i],
                        Precipitacao = d1h.precipitation[i],
                        PrecipitacaoProb = d1h.precipitation_probability[i],
                        VentoVelocidade = d1h.wind_speed_10m[i],
                        VentoRajada = d1h.wind_gusts_10m[i],
                        VentoDirecao = (int)d1h.wind_direction_10m[i],
                        Pressao = d1h.surface_pressure[i],
                        PictoCode = -1,
                        WMOCode = d1h.weather_code[i],
                        RegionCode = local.Code
                    });
                }

                db.RegistraWeather(lst);
                logger.Information("[WeatherOpenMeteo] {region} Registrados {qtd} horários", local.Code, lst.Count);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "[WeatherOpenMeteo] {region} Error {msg}", local.Code, ex.Message);
            }
        }
    }

    private bool temRecente(string regiao, out TimeSpan age, out bool temChuva)
    {
        var lista = db.ObterWeatherProximasHoras(regiao, hour: 6).ToArray();
        if (lista.Length == 0)
        {
            age = TimeSpan.FromDays(7);
            temChuva = false;
            return false; // Nunca teve
        }

        var coletaMax = lista.Max(o => o.ColetaUTC);
        age = DateTime.UtcNow - coletaMax;

        if (lista.Any(o => o.Precipitacao > 0.1M)) // vai ter chuva
        {
            temChuva = true;
            return age.TotalHours < HOURS_CHUVA;
        }
        else
        {
            temChuva = false;
            return age.TotalHours < HOURS_SECO;
        }
    }

}
