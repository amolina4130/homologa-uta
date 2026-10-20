using HomologaUTA.Documentos.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "HomologaUTA - Documentos",
        Version = "v1",
        Description = "Servicio de documentos de las solicitudes"
    });
});
builder.Services.AddDbContext<DocumentosDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Urls.Add("http://0.0.0.0:8080");

app.Run();