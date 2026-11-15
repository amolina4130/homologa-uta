using HomologaUTA.Autenticacion.Data;
using HomologaUTA.Autenticacion.Models;
using HomologaUTA.Autenticacion.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "HomologaUTA - Autenticación",
        Version = "v1",
        Description = "Servicio de autenticación y roles"
    });
});
builder.Services.AddDbContext<AuthDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
builder.Services.AddScoped<TokenService>();

var app = builder.Build();


app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

// En Docker usa 8080; en local se puede cambiar con la variable PORT.
var puerto = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://0.0.0.0:{puerto}");

// Ejecutar la precarga de datos (usuarios semilla)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var context = services.GetRequiredService<AuthDbContext>();

        // En una base nueva las tablas no existen: se aplican las migraciones antes de sembrar.
        const int maxIntentos = 10;
        for (var intento = 1; ; intento++)
        {
            try
            {
                context.Database.Migrate();
                break;
            }
            catch (Exception ex) when (intento < maxIntentos)
            {
                logger.LogWarning(ex,
                    "No se pudo migrar la base de autenticación (intento {Intento}/{Max}). Reintentando en 3 s...",
                    intento, maxIntentos);
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }

        var hasher = services.GetRequiredService<IPasswordHasher<Usuario>>();
        var config = services.GetRequiredService<IConfiguration>();

        await SeedData.InitializeAsync(context, hasher, config, logger);
    }
    catch (Exception ex)
    {
        // No se deja caer el contenedor: el servicio queda arriba y el error se ve en los logs.
        logger.LogError(ex, "No se pudo inicializar la base de datos de autenticación.");
    }
}

app.Run();