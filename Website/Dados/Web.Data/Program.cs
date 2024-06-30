using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("data/logs/log.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSerilog();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
addDatabase(builder.Services, builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
app.UseSwagger();
app.UseSwaggerUI();
//}

app.UseStaticFiles();
app.UseAuthorization();

app.MapControllers();

app.Run();


void addDatabase(IServiceCollection services, Microsoft.Extensions.Configuration.ConfigurationManager configuration)
{
    var path = configuration["Database"] ?? "data/data.db";
    var db = new Web.Data.DAO.DB(path);
    db.Setup();
    services.AddSingleton(db);
}
