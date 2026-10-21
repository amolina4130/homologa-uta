using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

//---------------------------------------------------------------
// 1) YARP: las rutas y destinos viven en yarp.json
// ---------------------------------------------------------------
builder.Configuration.AddJsonFile("yarp.json", optional: false, reloadOnChange: true);

if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true")
    builder.Configuration.AddJsonFile("docker.settings.json", optional: false);
    
builder.Configuration.AddEnvironmentVariables();
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// ---------------------------------------------------------------
// 2) JWT: el Gateway solo valida el token; lo emite el servicio de autenticación.
// ---------------------------------------------------------------
var jwt = builder.Configuration.GetSection("Jwt");
var key = builder.Configuration["JWT_SECRET"] ?? jwt["Key"]
    ?? throw new InvalidOperationException("Falta la configuración Jwt:Key");
var issuer = builder.Configuration["JWT_ISSUER"] ?? jwt["Issuer"];
var audience = builder.Configuration["JWT_AUDIENCE"] ?? jwt["Audience"];

builder.Services
    .AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.MapInboundClaims = false; // conserva el claim "rol" tal cual
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = "rol",
            NameClaimType = "sub"
        };
    });

// ---------------------------------------------------------------
// 3) Políticas de autorización por rol
// ---------------------------------------------------------------
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy("autenticado", p => p.RequireAuthenticatedUser())
    .AddPolicy("estudiante", p => p.RequireRole("ESTUDIANTE"))
    .AddPolicy("coordinador", p => p.RequireRole("COORDINADOR"))
    .AddPolicy("secretaria", p => p.RequireRole("SECRETARIA"))
    .AddPolicy("consejo", p => p.RequireRole("CONSEJO"))
    .AddPolicy("administrador", p => p.RequireRole("ADMINISTRADOR"));

// ---------------------------------------------------------------
// 4) CORS: FORZADO PARA EL FRONTEND
// ---------------------------------------------------------------
var origins = new[] { "http://localhost:3000", "http://127.0.0.1:3000" };
builder.Services.AddCors(options =>
    options.AddPolicy("frontend", policy => policy
        .WithOrigins(origins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

// ---------------------------------------------------------------
// 5) Cliente HTTP para consultar el /health de cada servicio
// ---------------------------------------------------------------
builder.Services.AddHttpClient("health", client => client.Timeout = TimeSpan.FromSeconds(2));

var app = builder.Build();

// IMPORTANTE: UseCors debe ir antes de Authentication y Authorization
app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();

// ---------------------------------------------------------------
// GET /health
// ---------------------------------------------------------------
app.MapGet("/health", async (IHttpClientFactory factory, IConfiguration config) =>
{
    var targets = config.GetSection("Health:Services").Get<List<ServiceTarget>>() ?? new();
    var client = factory.CreateClient("health");

    var servicios = await Task.WhenAll(targets.Select(async target =>
    {
        var reloj = Stopwatch.StartNew();
        try
        {
            using var respuesta = await client.GetAsync(target.Url);
            reloj.Stop();
            return respuesta.IsSuccessStatusCode
                ? new ComponentStatus(target.Name, target.Port, "EN_LINEA", reloj.ElapsedMilliseconds)
                : new ComponentStatus(target.Name, target.Port, "SIN_RESPUESTA", null);
        }
        catch (Exception)
        {
            return new ComponentStatus(target.Name, target.Port, "SIN_RESPUESTA", null);
        }
    }));

    var gateway = new ComponentStatus("API Gateway (YARP)", 8080, "EN_LINEA", 0);
    return Results.Ok(new { componentes = new[] { gateway }.Concat(servicios) });
}).AllowAnonymous();

app.MapReverseProxy();

app.Run();

// Tipos auxiliares
public class ServiceTarget
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public int Port { get; set; }
}

public record ComponentStatus(string Nombre, int Puerto, string Estado, long? RespuestaMs);