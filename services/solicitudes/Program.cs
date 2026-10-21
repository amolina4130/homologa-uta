using HomologaUTA.Solicitudes.Data;
using Microsoft.EntityFrameworkCore;

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

var app = builder.Build();

// --- 4. USAR CORS ---
app.UseCors("AllowAll");

// --- 5. SWAGGER ---
app.UseSwagger();
app.UseSwaggerUI();

// --- 6. MAPEAR CONTROLADORES ---
app.MapControllers();

app.Urls.Add("http://0.0.0.0:8080");

// --- 7. MIGRACIONES Y DATOS SEMILLA ---
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