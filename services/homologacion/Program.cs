using HomologaUTA.Homologacion.Agents;
using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.Connectors;
using HomologaUTA.Homologacion.Data;
using HomologaUTA.Homologacion.Services;
using HomologaUTA.Homologacion.Validadores;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Controladores y Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "HomologaUTA - Homologación",
        Version = "v1",
        Description = "Validación de reglas objetivas y análisis de homologación"
    });

    // Comentarios XML de los controladores y DTOs (se generan al compilar)
    var xml = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
    if (File.Exists(xml)) c.IncludeXmlComments(xml);
});

// 2. Base de datos del servicio (resultados del análisis)
builder.Services.AddDbContext<HomologacionDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 3. Validadores de reglas objetivas (TA-07.1 y TA-07.2) y el servicio que los ejecuta (TA-07.4)
builder.Services.AddSingleton<ValidadorReglas>();
builder.Services.AddSingleton<ValidadorDocumentos>();
builder.Services.AddSingleton<ServicioValidacionSolicitud>();
builder.Services.AddSingleton<DecisionHomologacion>();

// 4. Registrar el cliente de Ollama
// El modelo real puede tardar decenas de segundos en responder cada asignatura;
// el tiempo de espera por petición debe ser holgado para no cortar el análisis.
builder.Services.AddHttpClient<OllamaClient>(c => c.Timeout = TimeSpan.FromMinutes(5));
// 5. Registrar el Agente Interno
builder.Services.AddScoped<AgenteInterno>();

// 6. Análisis de la solicitud y publicación del evento AnalisisCompletado (TA-08.4)
builder.Services.AddSingleton<IAnalisisEventPublisher, AnalisisEventPublisher>();
builder.Services.AddScoped<AnalisisService>();


// 7. Conector del agente externo y su servicio (TA-09.2)
builder.Services.AddHttpClient<AgenteExternoConnector>();
builder.Services.AddHttpClient<DocumentosClient>();
builder.Services.AddScoped<AnalisisExternoService>();


// Endpoints del Coordinador (TA-10.2): consulta a Solicitudes y su servicio
builder.Services.AddHttpClient<SolicitudesClient>();
builder.Services.AddScoped<CoordinadorService>();


var app = builder.Build();

// 8. Configuración de la app
// SerializeAsV2: el visor de Swashbuckle 6.6.2 no reconoce "openapi 3.0.4"
app.UseSwagger(c => c.SerializeAsV2 = true);
app.UseSwaggerUI();

app.MapControllers();

// En Docker usa 8080; en local se puede cambiar con la variable PORT.
var puerto = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://0.0.0.0:{puerto}");

// 9. Aplicar migraciones al arrancar
using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<HomologacionDbContext>();
        context.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ocurrió un error al aplicar las migraciones de homologación.");
    }
}

app.Run();
