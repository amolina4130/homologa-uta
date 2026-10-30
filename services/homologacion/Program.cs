using HomologaUTA.Homologacion.Services;
using HomologaUTA.Homologacion.Validadores;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
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

// Validadores de reglas objetivas (TA-07.1 y TA-07.2) y el servicio que los ejecuta (TA-07.4)
builder.Services.AddSingleton<ValidadorReglas>();
builder.Services.AddSingleton<ValidadorDocumentos>();
builder.Services.AddSingleton<ServicioValidacionSolicitud>();

var app = builder.Build();

// Swagger siempre activo (igual que los demás servicios), también dentro de Docker.
app.UseSwagger(c => c.SerializeAsV2 = true);
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();