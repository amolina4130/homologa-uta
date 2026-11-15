using System.Text;
using HomologaUTA.Solicitudes.Data;
using HomologaUTA.Solicitudes.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// --- 1. CONFIGURACIÓN DE CORS ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// --- 2. CONTROLADORES Y SWAGGER ---
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

    var xml = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
    if (File.Exists(xml)) c.IncludeXmlComments(xml);
});

// --- 3. BASE DE DATOS ---
builder.Services.AddDbContext<SolicitudesDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- 4. SERVICIOS DEL CATÁLOGO Y MEMORIA ---
builder.Services.AddScoped<MemoriaService>();
builder.Services.AddScoped<CargaPlanesService>();

// --- 5. JWT: el Gateway valida el token y lo reenvía. Aquí leemos el claim "rol". ---
var jwtKey = builder.Configuration["JWT_SECRET"]
    ?? builder.Configuration["Jwt:Key"]
    ?? "clave-de-desarrollo-homologauta-cambiar-en-produccion-2026";

builder.Services
    .AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
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

// --- 6. CORS, JWT Y SWAGGER ---
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI();

// --- 7. MAPEAR CONTROLADORES ---
app.MapControllers();

app.Urls.Add("http://0.0.0.0:8080");

// --- 8. MIGRACIONES Y DATOS SEMILLA ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<SolicitudesDbContext>();
        context.Database.Migrate();
        await SeedData.InitializeAsync(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ocurrió un error al inicializar la base de datos.");
    }
}

app.Run();
