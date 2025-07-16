namespace Web.Data.BkgWorkers;

using Microsoft.Extensions.Hosting;
using Serilog;
using Simple.API;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Web.Data.DAO;

public class WeatherMeteoBlue : IHostedService, IDisposable
{
    internal static string API_KEY = string.Empty;

    private readonly ILogger logger;
    private readonly DB db;
    private readonly ClientInfo clientInfo;
    private Timer _timer;
    public WeatherMeteoBlue(DB db, ILogger logger)
    {
        this.db = db;
        this.logger = logger;
        clientInfo = new ClientInfo("https://my.meteoblue.com");
    }
    public void Dispose()
    {
        _timer?.Dispose();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(API_KEY))
        {
            logger.Information("[WeatherMeteoBlue] Não há API KEY, encerrando serviço");
            return;
        }

        logger.Information("[WeatherMeteoBlue] Iniciando serviço de meteorologia MeteoBlue...");
        _timer = new Timer(executaVerificacaoAsync, null, TimeSpan.FromHours(1), TimeSpan.FromHours(13));

        await Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    private async void executaVerificacaoAsync(object? state)
    {
        // Chega última
        if (temRecente())
        {
            logger.Information("[WeatherMeteoBlue] Tem recente, SKIP");
            return;
        }

        try
        {
            var url = montaUrl();
            var r = await clientInfo.GetAsync<DadosModel>(url);
            r.EnsureSuccessStatusCode();

            var d1h = r.Data.data_1h;
            var lst = new List<DAO.DBModels.TBWeather>();
            for (int i = 0; i < d1h.time.Length; i++)
            {
                lst.Add(new DAO.DBModels.TBWeather
                {
                    Id = 0,
                    ColetaUTC = pegaData(r.Data.metadata.modelrun_updatetime_utc, 0), // 0: Já é UTC
                    Lat = r.Data.metadata.latitude,
                    Lon = r.Data.metadata.longitude,

                    ForecastUTC = pegaData(d1h.time[i], +3), // É Local, ajustar
                    LuzDia = d1h.isdaylight[i] == 1,
                    UvIndex = d1h.uvindex[i],
                    Temperatura = d1h.temperature[i],
                    SensacaoTermica = d1h.felttemperature[i],
                    Umidade = d1h.relativehumidity[i],
                    Precipitacao = d1h.precipitation[i],
                    PrecipitacaoProb = d1h.precipitation_probability[i],
                    VentoVelocidade = d1h.windspeed[i],
                    VentoDirecao = d1h.winddirection[i],
                    Pressao = d1h.sealevelpressure[i],
                    PictoCode = d1h.pictocode[i],
                });
            }

            db.RegistraWeather(lst);
            logger.Information("[WeatherMeteoBlue] Registrados {qtd} horários", lst.Count);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[WeatherMeteoBlue] Error {msg}", ex.Message);
        }
    }
    private bool temRecente()
    {
        var lista = db.ObterWeatherProximasHoras().ToArray();
        if (lista.Length == 0) return false; // Nunca teve

        var coletaMax = lista.Max(o => o.ColetaUTC);
        var age = DateTime.UtcNow - coletaMax;

        return age.TotalHours < 4; // Não mais rápido que 4h
    }
    private DateTime pegaData(string strDate, int tz)
    {
        var dt = DateTime.Parse(strDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
                         .ToUniversalTime()
                         .AddHours(tz) // Ajusta manualmente a tz
                         ; 
        return dt;
    }
    private string montaUrl()
    {
        if (API_KEY == "DEBUG")
        {
            logger.Information("[WeatherMeteoBlue] MODO DEBUG");
            // DEMO KEY
            return "/packages/basic-1h_agro-1h?lat=47.56&lon=7.57&apikey=DEMOKEY&sig=e85c990f1d5d476b29eddd989ca56859";
        }

        decimal lat = -29.64M;
        decimal lon = -50.57M;

        string sLat = lat.ToString(CultureInfo.InvariantCulture);
        string sLon = lon.ToString(CultureInfo.InvariantCulture);

        return $"/packages/basic-1h?lat={sLat}&lon={sLon}&apikey={API_KEY}";
    }

    public class DadosModel
    {
        public static readonly string[,] Pictocodes_1h = new string[,]
        {
            { "1", "Clear, cloudless sky", "Céu claro, sem nuvens", "bi-sun-fill" },
            { "2", "Clear, few cirrus", "Céu claro, poucas nuvens cirrus", "bi-sun" },
            { "3", "Clear with cirrus", "Céu claro com cirrus", "bi-cloud-sun" },
            { "4", "Clear with few low clouds", "Céu claro com poucas nuvens baixas", "bi-cloud" },
            { "5", "Clear with few low clouds and few cirrus", "Céu claro com poucas nuvens baixas e poucas cirrus", "bi-cloud-sun" },
            { "6", "Clear with few low clouds and cirrus", "Céu claro com poucas nuvens baixas e cirrus", "bi-cloud-sun-fill" },
            { "7", "Partly cloudy", "Parcialmente nublado", "bi-clouds" },
            { "8", "Partly cloudy and few cirrus", "Parcialmente nublado com poucas cirrus", "bi-clouds" },
            { "9", "Partly cloudy and cirrus", "Parcialmente nublado com cirrus", "bi-clouds-fill" },
            { "10", "Mixed with some thunderstorm clouds possible", "Misto com possibilidade de nuvens de tempestade", "bi-cloud-lightning" },
            { "11", "Mixed with few cirrus with some thunderstorm clouds possible", "Misto com poucas cirrus e possibilidade de nuvens de tempestade", "bi-cloud-lightning" },
            { "12", "Mixed with cirrus with some thunderstorm clouds possible", "Misto com cirrus e possibilidade de nuvens de tempestade", "bi-cloud-lightning-fill" },
            { "13", "Clear but hazy", "Céu claro, mas com névoa", "bi-cloud-haze" },
            { "14", "Clear but hazy with few cirrus", "Céu claro, mas com névoa e poucas cirrus", "bi-cloud-haze" },
            { "15", "Clear but hazy with cirrus", "Céu claro, mas com névoa e cirrus", "bi-cloud-haze-fill" },
            { "16", "Fog/low stratus clouds", "Nevoeiro/nuvens estratos baixas", "bi-cloud-fog" },
            { "17", "Fog/low stratus clouds with few cirrus", "Nevoeiro/nuvens estratos baixas com poucas cirrus", "bi-cloud-fog" },
            { "18", "Fog/low stratus clouds with cirrus", "Nevoeiro/nuvens estratos baixas com cirrus", "bi-cloud-fog-fill" },
            { "19", "Mostly cloudy", "Predominantemente nublado", "bi-clouds-fill" },
            { "20", "Mostly cloudy and few cirrus", "Predominantemente nublado com poucas cirrus", "bi-clouds" },
            { "21", "Mostly cloudy and cirrus", "Predominantemente nublado com cirrus", "bi-clouds-fill" },
            { "22", "Overcast", "Encoberto", "bi-cloud-fill" },
            { "23", "Overcast with rain", "Encoberto com chuva", "bi-cloud-rain" },
            { "24", "Overcast with snow", "Encoberto com neve", "bi-cloud-snow" },
            { "25", "Overcast with heavy rain", "Encoberto com chuva forte", "bi-cloud-rain-heavy" },
            { "26", "Overcast with heavy snow", "Encoberto com neve forte", "bi-cloud-snow-fill" },
            { "27", "Rain, thunderstorms likely", "Chuva, probabilidade de trovoadas", "bi-cloud-lightning-rain" },
            { "28", "Light rain, thunderstorms likely", "Chuva leve, probabilidade de trovoadas", "bi-cloud-drizzle" },
            { "29", "Storm with heavy snow", "Tempestade com neve forte", "bi-cloud-snow-fill" },
            { "30", "Heavy rain, thunderstorms likely", "Chuva forte, probabilidade de trovoadas", "bi-cloud-lightning-rain" },
            { "31", "Mixed with showers", "Misto com pancadas de chuva", "bi-cloud-rain" },
            { "32", "Mixed with snow showers", "Misto com pancadas de neve", "bi-cloud-snow" },
            { "33", "Overcast with light rain", "Encoberto com chuva leve", "bi-cloud-drizzle" },
            { "34", "Overcast with light snow", "Encoberto com neve leve", "bi-cloud-snow" },
            { "35", "Overcast with mixture of snow and rain", "Encoberto com mistura de neve e chuva", "bi-cloud-sleet" },
            { "36", "Not used", "Não utilizado", "bi-x" },
            { "37", "Not used", "Não utilizado", "bi-x" }
        };

        public Metadata metadata { get; set; }
        public Units units { get; set; }
        public Data_1H data_1h { get; set; }

        public class Metadata
        {
            public string modelrun_updatetime_utc { get; set; }
            public string name { get; set; }
            public int height { get; set; }
            public string timezone_abbrevation { get; set; }
            public decimal latitude { get; set; }
            public string modelrun_utc { get; set; }
            public decimal longitude { get; set; }
            public decimal utc_timeoffset { get; set; }
            public decimal generation_time_ms { get; set; }
        }

        public class Units
        {
            public string transpiration { get; set; }
            public string soilmoisture { get; set; }
            public string leafwetness { get; set; }
            public string windspeed { get; set; }
            public string precipitation_probability { get; set; }
            public string precipitation { get; set; }
            public string sensibleheatflux { get; set; }
            public string relativehumidity { get; set; }
            public string temperature { get; set; }
            public string time { get; set; }
            public string pressure { get; set; }
            public string winddirection { get; set; }
        }

        public class Data_1H
        {
            public string[] time { get; set; }
            public decimal[] temperature { get; set; }
            public decimal[] windspeed { get; set; }
            public int[] precipitation_probability { get; set; }
            public decimal[] felttemperature { get; set; }
            public decimal[] precipitation { get; set; }
            public int[] isdaylight { get; set; }
            public int[] uvindex { get; set; }
            public int[] relativehumidity { get; set; }
            public decimal[] sealevelpressure { get; set; }
            public int[] winddirection { get; set; }
            public int[] pictocode { get; set; } // versão para 1h, não diário

            // Não usa, remove para evitar problemas
            //public decimal[] dewpointtemperature { get; set; }
            //public decimal[] convective_precipitation { get; set; }
            //public decimal[] sensibleheatflux { get; set; }
            //public string[] rainspot { get; set; }
            //public decimal[] wetbulbtemperature { get; set; }
            //public decimal[] potentialevapotranspiration { get; set; }
            //public decimal[] skintemperature { get; set; }
            //public decimal[] snowfraction { get; set; }
            //public decimal[] leafwetnessindex { get; set; }
        }
    }

    // PictoCodes para 1h (diário é diferente)
    // 1: Clear, cloudless sky  
    // 2: Clear, few cirrus  
    // 3: Clear with cirrus  
    // 4: Clear with few low clouds  
    // 5: Clear with few low clouds and few cirrus  
    // 6: Clear with few low clouds and cirrus  
    // 7: Partly cloudy  
    // 8: Partly cloudy and few cirrus  
    // 9: Partly cloudy and cirrus  
    // 10: Mixed with some thunderstorm clouds possible  
    // 11: Mixed with few cirrus with some thunderstorm clouds possible  
    // 12: Mixed with cirrus with some thunderstorm clouds possible  
    // 13: Clear but hazy  
    // 14: Clear but hazy with few cirrus  
    // 15: Clear but hazy with cirrus  
    // 16: Fog/low stratus clouds  
    // 17: Fog/low stratus clouds with few cirrus  
    // 18: Fog/low stratus clouds with cirrus  
    // 19: Mostly cloudy  
    // 20: Mostly cloudy and few cirrus  
    // 21: Mostly cloudy and cirrus  
    // 22: Overcast  
    // 23: Overcast with rain  
    // 24: Overcast with snow  
    // 25: Overcast with heavy rain  
    // 26: Overcast with heavy snow  
    // 27: Rain, thunderstorms likely  
    // 28: Light rain, thunderstorms likely  
    // 29: Storm with heavy snow  
    // 30: Heavy rain, thunderstorms likely  
    // 31: Mixed with showers  
    // 32: Mixed with snow showers  
    // 33: Overcast with light rain  
    // 34: Overcast with light snow  
    // 35: Overcast with mixture of snow and rain  
    // 36: Not used  
    // 37: Not used

}
