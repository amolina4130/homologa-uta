using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HomologaUTA.Homologacion.Tests;

/// <summary>
/// Solicitudes ya no copia la asignatura de origen: envía un centinela que el análisis debe
/// resolver contra la memoria de la carrera destino (HU-14/HU-21).
/// </summary>
public class PreseleccionSinEquivalenteTests
{
    private static PreseleccionService CrearServicio(List<MemoriaAsignaturaDto> destino)
    {
        var memoria = new Mock<IMemoriaClient>();
        memoria.Setup(m => m.ObtenerAsignaturasAsync(2, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(destino);

        return new PreseleccionService(
            db: null!,
            solicitudes: null!,
            memoria: memoria.Object,
            config: new PreseleccionConfig(),
            logger: NullLogger<PreseleccionService>.Instance);
    }

    private static (SolicitudCompletaDto Info, List<AsignaturaAnalisisDto> Entradas) CrearEntrada()
    {
        var info = new SolicitudCompletaDto { CarreraDestinoId = 2 };
        var entradas = new List<AsignaturaAnalisisDto>
        {
            new()
            {
                AsignaturaOrigen = "Automatización y Robótica",
                ContenidoOrigen = "PLAN_ORIGEN",
                AsignaturaDestino = PreseleccionService.SinEquivalenteEnMallaDestino,
                ContenidoDestino = string.Empty,
                AsignaturaDestinoId = null
            }
        };
        return (info, entradas);
    }

    [Fact]
    public async Task ConCandidataEnLaMemoria_ReemplazaElCentinelaPorUnaAsignaturaReal()
    {
        var servicio = CrearServicio(new List<MemoriaAsignaturaDto>
        {
            new()
            {
                AsignaturaId = 7,
                Nombre = "Tecnologías de la Información (TI)",
                Area = "Electrónica",
                Nivel = "Tercero",
                ContenidosMinimos = "CONTENIDO_MEMORIA"
            }
        });
        var (info, entradas) = CrearEntrada();

        var sinCandidata = await servicio.ResolverSinEquivalenteAsync(info, entradas);

        Assert.Empty(sinCandidata);
        var entrada = Assert.Single(entradas);
        Assert.Equal("Tecnologías de la Información (TI)", entrada.AsignaturaDestino);
        Assert.Equal(7, entrada.AsignaturaDestinoId);
        Assert.Equal("CONTENIDO_MEMORIA", entrada.ContenidoDestino);
    }

    [Fact]
    public async Task SinCandidata_DevuelveLaMateriaParaRevision()
    {
        var servicio = CrearServicio(new List<MemoriaAsignaturaDto>());
        var (info, entradas) = CrearEntrada();

        var sinCandidata = await servicio.ResolverSinEquivalenteAsync(info, entradas);

        Assert.Empty(entradas);
        var pendiente = Assert.Single(sinCandidata);
        Assert.Equal("Automatización y Robótica", pendiente.MateriaOrigen);
        Assert.Contains("No hay una asignatura candidata", pendiente.Motivo, StringComparison.Ordinal);
    }
}
