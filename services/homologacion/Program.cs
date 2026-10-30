using HomologaUTA.Homologacion.Clients;

var builder = WebApplication.CreateBuilder(args);

// 1. Controladores y Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ESTA ES LA LÍNEA QUE FALTA PARA QUE SWAGGER SE RENDERICE BIEN
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "HomologaUTA - Homologación",
        Version = "v1",
        Description = "Servicio orquestador de homologación y agente de IA"
    });
});

// 2. Registrar el cliente de Ollama
builder.Services.AddHttpClient<OllamaClient>();
// 3. Registrar el Agente Interno
builder.Services.AddScoped<Agents.AgenteInterno>();
var app = builder.Build();

// 4. Configuración de la app
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Urls.Add("http://0.0.0.0:8080");

app.Run();