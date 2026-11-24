using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace catalogo.Tests;

public record CargaResponse(int Cargadas, bool SinCambios, string Huella);

public class MemoriaTests
{
    private const string RolPermitido = "Administrador"; // AJUSTAR según #81
    private const string RolNoPermitido = "Estudiante";  // AJUSTAR

    [Fact]
    public async Task PrimeraCarga_DevuelveSinCambiosFalse()
    {
        using var factory = new CatalogoFactory();
        var client = factory.CrearCliente(RolPermitido);

        var resp = await client.PostAsync("/memoria/carga", null);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<CargaResponse>();
        body!.SinCambios.Should().BeFalse();
        body.Cargadas.Should().BeGreaterThan(0);
        body.Huella.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RepeticionSinCambios_DevuelveSinCambiosTrue_YMismaHuella()
    {
        using var factory = new CatalogoFactory();
        var client = factory.CrearCliente(RolPermitido);

        var primera = await (await client.PostAsync("/memoria/carga", null))
            .Content.ReadFromJsonAsync<CargaResponse>();
        var segunda = await (await client.PostAsync("/memoria/carga", null))
            .Content.ReadFromJsonAsync<CargaResponse>();

        segunda!.SinCambios.Should().BeTrue();
        segunda.Huella.Should().Be(primera!.Huella);
    }

    [Fact]
    public async Task CargaTrasCambiarDato_CambiaLaHuella()
    {
        using var factory = new CatalogoFactory();
        var client = factory.CrearCliente(RolPermitido);

        var primera = await (await client.PostAsync("/memoria/carga", null))
            .Content.ReadFromJsonAsync<CargaResponse>();

        // Modificar un dato de origen
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogoDbContext>();
            var asignatura = db.Asignaturas.First();   // AJUSTAR nombre del DbSet
            asignatura.Nombre += " (modificada)";      // AJUSTAR propiedad
            await db.SaveChangesAsync();
        }

        var segunda = await (await client.PostAsync("/memoria/carga", null))
            .Content.ReadFromJsonAsync<CargaResponse>();

        segunda!.SinCambios.Should().BeFalse();
        segunda.Huella.Should().NotBe(primera!.Huella);
    }

    [Fact]
    public async Task AccesoConOtroRol_Devuelve403()
    {
        using var factory = new CatalogoFactory();
        var client = factory.CrearCliente(RolNoPermitido);

        var resp = await client.PostAsync("/memoria/carga", null);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task FiltroNivelMaximo_SoloDevuelveAsignaturasHastaEseNivel()
    {
        using var factory = new CatalogoFactory();
        var client = factory.CrearCliente(RolPermitido);
        await client.PostAsync("/memoria/carga", null);

        var carreraId = 1; // AJUSTAR a un id existente en sus datos semilla
        var resp = await client.GetAsync(
            $"/memoria/carreras/{carreraId}/asignaturas?nivelMaximo=3");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var lista = await resp.Content.ReadFromJsonAsync<List<AsignaturaDto>>();
        lista.Should().NotBeEmpty();
        lista!.Should().OnlyContain(a => a.Nivel <= 3);
    }
}

public record AsignaturaDto(int Id, string Nombre, int Nivel); // AJUSTAR a la respuesta real