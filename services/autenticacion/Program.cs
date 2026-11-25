using HomologaUTA.Autenticacion.Data;
using HomologaUTA.Autenticacion.Models;
using HomologaUTA.Autenticacion.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

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

// Valida el JWT que emite este mismo servicio. Sin esto, los endpoints con [Authorize]
// (como PUT /auth/perfil) fallaban con 500 por no tener esquema de autenticación.
var jwtKey = builder.Configuration["JWT_SECRET"] ?? builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["JWT_ISSUER"] ?? builder.Configuration["Jwt:Issuer"] ?? "HomologaUTA";
var jwtAudience = builder.Configuration["JWT_AUDIENCE"] ?? builder.Configuration["Jwt:Audience"] ?? "HomologaUTA";

if (!string.IsNullOrWhiteSpace(jwtKey))
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,
                ValidateAudience = true,
                ValidAudience = jwtAudience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = "sub",
                RoleClaimType = "rol"
            };
        });
}

var app = builder.Build();


app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

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