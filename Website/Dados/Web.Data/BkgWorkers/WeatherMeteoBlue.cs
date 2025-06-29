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
        _timer = new Timer(executaVerificacaoAsync, null, TimeSpan.FromSeconds(10), TimeSpan.FromHours(12));

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
                    ColetaUTC = pegaData(r.Data.metadata.modelrun_updatetime_utc),
                    Lat = r.Data.metadata.latitude,
                    Lon = r.Data.metadata.longitude,

                    ForecastUTC = pegaData(d1h.time[i]),
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
                });
            }

            db.RegistraWeather(lst);

        }
        catch (Exception ex)
        {
            logger.Error(ex, "[WeatherMeteoBlue] Error {msg}", ex.Message);
        }
    }
    private bool temRecente()
    {
        var lista = db.ObterWeatherProximasHoras();

        var coletaMax = lista.Max(o => o.ColetaUTC);
        var age = DateTime.UtcNow - coletaMax;

        return age.TotalHours < 2;
    }
    private DateTime pegaData(string strDate)
    {
        var dt = DateTime.Parse(strDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime();
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

        decimal lat = 0;
        decimal lon = 0;

        string sLat = lat.ToString(CultureInfo.InvariantCulture);
        string sLon = lon.ToString(CultureInfo.InvariantCulture);

        return $"/packages/basic-1h?lat={sLat}&lon={sLon}&apikey={API_KEY}";
    }

    public class DadosModel
    {
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

            // Não usa, remove para evitar problemas
            //public decimal[] dewpointtemperature { get; set; }
            //public decimal[] convective_precipitation { get; set; }
            //public decimal[] sensibleheatflux { get; set; }
            //public string[] rainspot { get; set; }
            //public int[] pictocode { get; set; }
            //public decimal[] wetbulbtemperature { get; set; }
            //public decimal[] potentialevapotranspiration { get; set; }
            //public decimal[] skintemperature { get; set; }
            //public decimal[] snowfraction { get; set; }
            //public decimal[] leafwetnessindex { get; set; }
        }
    }

}
