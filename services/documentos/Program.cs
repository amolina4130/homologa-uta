using HomologaUTA.Documentos.Data;
using HomologaUTA.Documentos.Services; // <-- Para el Extractor
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

// --- NUEVO: Registrar el servicio extractor de PDF ---
builder.Services.AddScoped<ExtractorPdfService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Urls.Add("http://0.0.0.0:8080");

// --- NUEVO: APLICAR MIGRACIONES AUTOMÁTICAMENTE ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<DocumentosDbContext>();
        context.Database.Migrate(); // Crea la tabla si no existe
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ocurrió un error al aplicar las migraciones de documentos.");
    }
}

app.Run();