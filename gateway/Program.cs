using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------
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
//    Issuer, Audience, Key y el nombre del claim de rol ("role")
//    deben ser idénticos a los que use el servicio de autenticación.
// ---------------------------------------------------------------
var jwt = builder.Configuration.GetSection("Jwt");
var key = builder.Configuration["JWT_SECRET"] ?? jwt["Key"]
    ?? throw new InvalidOperationException("Falta la configuración Jwt:Key");
builder.Services
    .AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.MapInboundClaims = false; // conserva el claim "role" tal cual
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwt["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = "role",
            NameClaimType = "sub"
        };
    });

// ---------------------------------------------------------------
// 3) Políticas de autorización por rol (las usa AuthorizationPolicy en yarp.json).
//    La política de respaldo exige usuario autenticado: lo que no se
//    marque como "Anonymous" queda protegido.
// ---------------------------------------------------------------
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy("autenticado", p => p.RequireAuthenticatedUser())
    .AddPolicy("estudiante", p => p.RequireRole("Estudiante"))
    .AddPolicy("coordinador", p => p.RequireRole("Coordinador"))
    .AddPolicy("secretaria", p => p.RequireRole("Secretaria"))
    .AddPolicy("consejo", p => p.RequireRole("Consejo"))
    .AddPolicy("administrador", p => p.RequireRole("Administrador"));

// ---------------------------------------------------------------
// 4) CORS: permite que el frontend React llame al Gateway desde otro origen
// ---------------------------------------------------------------
var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
    options.AddPolicy("frontend", policy => policy
        .WithOrigins(origins)
        .AllowAnyHeader()
        .AllowAnyMethod()));

// ---------------------------------------------------------------
// 5) Cliente HTTP para consultar el /health de cada servicio (máx. 2 s por servicio)
// ---------------------------------------------------------------
builder.Services.AddHttpClient("health", client => client.Timeout = TimeSpan.FromSeconds(2));

var app = builder.Build();

app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();

// ---------------------------------------------------------------
// GET /health: estado de cada servicio, consultado desde el Gateway.
// Es público (lo usa la pantalla "Estado del sistema").
// Siempre responde 200: si un servicio no contesta, aparece "Sin respuesta"
// y los demás siguen "En línea".
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
            return new ServiceStatus(
                target.Name,
                respuesta.IsSuccessStatusCode ? "En línea" : "Sin respuesta",
                reloj.ElapsedMilliseconds);
        }
        catch (Exception)
        {
            return new ServiceStatus(target.Name, "Sin respuesta", null);
        }
    }));

    var general = servicios.All(s => s.Estado == "En línea") ? "Healthy" : "Degraded";
    return Results.Ok(new { estado = general, gateway = "En línea", servicios });
}).AllowAnonymous();

app.MapReverseProxy();

app.Run();

// Tipos auxiliares (deben ir después de las instrucciones de nivel superior)
public class ServiceTarget
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
}

public record ServiceStatus(string Nombre, string Estado, long? TiempoMs);