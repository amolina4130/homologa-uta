using HomologaUTA.Autenticacion.Data;
using HomologaUTA.Autenticacion.Models;
using HomologaUTA.Autenticacion.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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

var app = builder.Build();


app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Urls.Add("http://0.0.0.0:8080");

// Ejecutar la precarga de datos (usuarios semilla)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AuthDbContext>();
    var hasher = services.GetRequiredService<IPasswordHasher<Usuario>>();
    var config = services.GetRequiredService<IConfiguration>();
    
    await SeedData.InitializeAsync(context, hasher, config);
}

app.Run();