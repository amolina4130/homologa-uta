using HomologaUTA.Homologacion.Agents;
using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.Connectors;
using HomologaUTA.Homologacion.Data;
using HomologaUTA.Homologacion.Services;
using HomologaUTA.Homologacion.Validadores;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

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
builder.Services.AddHttpClient<OllamaClient>();
// 5. Registrar el Agente Interno
builder.Services.AddScoped<AgenteInterno>();

// 6. Análisis de la solicitud y publicación del evento AnalisisCompletado (TA-08.4)
builder.Services.AddSingleton<IAnalisisEventPublisher, AnalisisEventPublisher>();
builder.Services.AddScoped<AnalisisService>();


// 7. Conector del agente externo y su servicio (TA-09.2)
builder.Services.AddHttpClient<AgenteExternoConnector>();
builder.Services.AddScoped<AnalisisExternoService>();


// Endpoints del Coordinador (TA-10.2): consulta a Solicitudes y su servicio
builder.Services.AddHttpClient<SolicitudesClient>();
builder.Services.AddScoped<CoordinadorService>();

// 8. Catálogo (HU-13/HU-14/HU-19), Documentos (HU-20) y nuevo flujo con memoria (HU-21/HU-22)
builder.Services.AddHttpClient<ICatalogoClient, CatalogoClient>();
builder.Services.AddHttpClient<DocumentosClient>();
builder.Services.AddScoped<PreseleccionService>();
builder.Services.AddScoped<AgenteExtractorRecord>();
builder.Services.AddScoped<ExtractorRecordService>();
builder.Services.AddScoped<AnalisisMemoriaService>();
builder.Services.AddScoped<ResolucionService>();
builder.Services.AddScoped<IInformeService, InformeService>();

// 9. JWT: el Gateway valida el token y lo reenvía; aquí se leen los claims "rol" y "email".
var jwtKey = builder.Configuration["JWT_SECRET"]
    ?? builder.Configuration["Jwt:Key"]
    ?? "clave-de-desarrollo-homologauta-cambiar-en-produccion-2026";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = "rol"
        };
    });

var app = builder.Build();

// 10. Configuración de la app
// SerializeAsV2: el visor de Swashbuckle 6.6.2 no reconoce "openapi 3.0.4"
app.UseAuthentication();
app.UseAuthorization();
app.UseSwagger(c => c.SerializeAsV2 = true);
app.UseSwaggerUI();

app.MapControllers();

app.Urls.Add("http://0.0.0.0:8080");

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
