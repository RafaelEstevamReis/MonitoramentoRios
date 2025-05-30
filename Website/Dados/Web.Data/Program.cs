using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Simple.Sqlite;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("data/logs/log.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSerilog();
builder.Services.AddSingleton(Log.Logger);
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowGetMethod", policy =>
    {
        policy.WithMethods("GET")    // Permite apenas o m�todo GET
              .AllowAnyOrigin()      // Permite qualquer origem
              .AllowAnyHeader();     // Permite qualquer cabe�alho
    });
});
// Add services to the container.
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true; // N�o faz diferen�a, HTTPS no proxy
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
addDatabase(builder.Services, builder.Configuration);

builder.Services.AddHostedService<Web.Data.BkgWorkers.OldDataArchiver>();
builder.Services.AddHostedService(i =>
{
    var host = builder.Configuration["mqtt_server"];
    var user = builder.Configuration["mqtt_user"];
    var pass = builder.Configuration["mqtt_pass"];
    return new Web.Data.BkgWorkers.MqttWorker(i.GetService<ILogger>(), i.GetService<Web.Data.DAO.DB>(), host, user, pass);
});
Web.Data.Controllers.Maintenance.ApiKey = builder.Configuration["maintenance-key"] ?? "";

var app = builder.Build();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
app.UseSwagger();
app.UseSwaggerUI();
//}

// Habilita o CORS para todas as rotas com a pol�tica "AllowGetMethod"
app.UseCors("AllowGetMethod");

app.UseResponseCompression();
app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers();

app.Run();

void addDatabase(IServiceCollection services, Microsoft.Extensions.Configuration.ConfigurationManager configuration)
{
    var path = configuration["Database"] ?? "data/data.db";
    var db = new Web.Data.DAO.DB(path);
    db.Setup();

    using (var cnn = ConnectionFactory.CreateConnection(path))
    {
        cnn.Execute("VACUUM");
        Log.Logger.Information("DB VACUUM Executed");
    }

    services.AddSingleton(db);
}
