using HomologaUTA.Documentos.Data;
using HomologaUTA.Documentos.Services;
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

// Conexión a Base de Datos
builder.Services.AddDbContext<DocumentosDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- Registrar servicios ---
builder.Services.AddScoped<ExtractorPdfService>();
builder.Services.AddScoped<EventPublisherService>(); // <--- NUEVO: Publicador de RabbitMQ

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Urls.Add("http://0.0.0.0:8080");

// --- APLICAR MIGRACIONES AUTOMÁTICAMENTE ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<DocumentosDbContext>();
        context.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ocurrió un error al aplicar las migraciones de documentos.");
    }
}

app.Run();