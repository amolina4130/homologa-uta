using HomologaUTA.Solicitudes.Data;
using HomologaUTA.Solicitudes.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

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
});

// --- 3. BASE DE DATOS ---
builder.Services.AddDbContext<SolicitudesDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- 3.1 JWT: el Gateway valida el token; aquí se leen los claims (correo y rol) ---
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
                RoleClaimType = "rol",
                NameClaimType = "sub"
            };
        });
}
else
{
    // Sin JWT configurado (pruebas locales) no se exige token; los controladores usan el correo demo.
    builder.Services.AddAuthentication();
}

// --- 3.2 Servicios del catálogo/memoria (HU-13, HU-19) ---
builder.Services.AddScoped<CargaPlanesService>();
builder.Services.AddScoped<MemoriaService>();

var app = builder.Build();

// --- 4. USAR CORS ---
app.UseCors("AllowAll");
app.UseAuthentication();

// --- 5. SWAGGER ---
app.UseSwagger();
app.UseSwaggerUI();

// --- 6. MAPEAR CONTROLADORES ---
app.MapControllers();

// En Docker usa 8080; en local se puede cambiar con la variable PORT.
var puerto = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://0.0.0.0:{puerto}");

// --- 7. MIGRACIONES Y DATOS SEMILLA ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<SolicitudesDbContext>();
        context.Database.Migrate();
        await SeedData.InitializeAsync(context);

        // HU-19: la memoria relacional del agente se carga al arrancar (y se
        // actualiza sola si cambió el catálogo gracias a la huella).
        var memoria = services.GetRequiredService<MemoriaService>();
        await memoria.CargarAsync();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ocurrió un error al inicializar la base de datos.");
    }
}

app.Run();