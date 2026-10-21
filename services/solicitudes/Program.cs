using HomologaUTA.Solicitudes.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "HomologaUTA - Solicitudes",
        Version = "v1",
        Description = "Servicio de solicitudes y catálogo de carreras"
    });
});
builder.Services.AddDbContext<SolicitudesDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Urls.Add("http://0.0.0.0:8080");

// ==========================================
// AGREGAR ESTE BLOQUE AQUÍ (Antes de app.Run)
// ==========================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<SolicitudesDbContext>();
        
        // 1. Aplica las migraciones pendientes (crea las tablas si no existen)
        context.Database.Migrate();
        
        // 2. Ejecuta tu lógica para cargar los JSON
        await SeedData.InitializeAsync(context); 
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ocurrió un error al inicializar la base de datos.");
    }
}
// ==========================================

app.Run();