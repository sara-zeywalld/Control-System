using ControlSystem.Api.Middleware;
using ControlSystem.BL.Ai;
using ControlSystem.BL.Controls;
using ControlSystem.BL.Definitions;
using ControlSystem.BL.Querying;
using ControlSystem.DAL;
using ControlSystem.DAL.Ai;
using ControlSystem.DAL.Persistence;

var builder = WebApplication.CreateBuilder(args);

var db = builder.Configuration.GetSection("ConnectionStrings").Get<DatabaseOptions>() ?? new DatabaseOptions();
if (string.IsNullOrWhiteSpace(db.DataReadOnly)) db.DataReadOnly = db.Metadata;
var ai = builder.Configuration.GetSection("Ai").Get<AiOptions>() ?? new AiOptions();

builder.Services.AddControllers().AddJsonOptions(o => ControlJson.Configure(o.JsonSerializerOptions));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDataAccess(db, ai);
builder.Services.AddSingleton<IQueryBuilder, SqlServerQueryBuilder>();
builder.Services.AddSingleton<ControlService>();
builder.Services.AddSingleton<ControlRunner>();
builder.Services.AddTransient<AiControlService>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:4200"])
    .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors();
app.MapControllers();
app.Run();

public partial class Program;
