using HomologaUTA.Solicitudes.Controllers;
using HomologaUTA.Solicitudes.Data;
using HomologaUTA.Solicitudes.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomologaUTA.Solicitudes.Tests;

/// <summary>
/// Regresión del bug de la pantalla de análisis: el DTO repetía el nombre de la asignatura de
/// origen en "Asignatura destino" cuando no había equivalencia en la carrera destino (HU-14/HU-19).
/// </summary>
public class InternoControllerAnalisisTests
{
    private const string Origen = "Automatización y Robótica";
    private const string Destino = "Tecnologías de la Información (TI)";
    private const string SinEquivalente = "Sin equivalente en la malla destino";
    private const string PlanOrigen = "PLAN_ANALITICO_ORIGEN";
    private const string PlanDestino = "PLAN_ANALITICO_DESTINO";

    private static SolicitudesDbContext CrearContexto(int? idDestinoEquivalencia = null)
    {
        var options = new DbContextOptionsBuilder<SolicitudesDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDbInterno_" + Guid.NewGuid())
            .Options;
        var db = new SolicitudesDbContext(options);

        var carreraOrigen = new Carrera { Id = 1, Nombre = "Ingeniería Industrial" };
        var carreraDestino = new Carrera { Id = 2, Nombre = "Ingeniería de Sistemas" };
        var otraCarrera = new Carrera { Id = 3, Nombre = "Otra carrera" };
        db.Carreras.AddRange(carreraOrigen, carreraDestino, otraCarrera);

        var asignaturaOrigen = new Asignatura
        {
            Id = 1, Nombre = Origen, Nivel = "Tercero", Area = "Electrónica",
            Creditos = 4, Horas = 96, CarreraId = 1, Carrera = carreraOrigen
        };
        var asignaturaDestino = new Asignatura
        {
            Id = 2, Nombre = Destino, Nivel = "Cuarto", Area = "Programación",
            Creditos = 3, Horas = 80, CarreraId = 2, Carrera = carreraDestino
        };
        var asignaturaAjena = new Asignatura
        {
            Id = 3, Nombre = "Asignatura ajena a la malla destino", Nivel = "Cuarto", Area = "Programación",
            Creditos = 2, Horas = 64, CarreraId = 3, Carrera = otraCarrera
        };
        db.Asignaturas.AddRange(asignaturaOrigen, asignaturaDestino, asignaturaAjena);

        db.PlanesAnaliticos.AddRange(
            new PlanAnalitico { Id = 1, AsignaturaId = 1, Contenido = PlanOrigen },
            new PlanAnalitico { Id = 2, AsignaturaId = 2, Contenido = PlanDestino });

        var solicitud = new Solicitud
        {
            Id = 1,
            NumeroProceso = "SOL-TEST-0001",
            Tipo = "Cambio de carrera",
            CarreraOrigenId = 1,
            CarreraDestinoId = 2,
            CarreraOrigen = carreraOrigen,
            CarreraDestino = carreraDestino,
            EstudianteCorreo = "comite@uta.edu.ec",
            EstudianteNombre = "Estudiante Prueba",
            EstudianteCedula = "1234567890"
        };
        solicitud.Asignaturas.Add(new SolicitudAsignatura
        {
            Id = 1, SolicitudId = 1, Solicitud = solicitud,
            AsignaturaId = 1, Asignatura = asignaturaOrigen
        });
        db.Solicitudes.Add(solicitud);

        if (idDestinoEquivalencia.HasValue)
        {
            db.Equivalencias.Add(new Equivalencia
            {
                Id = 1,
                CarreraOrigenId = 1,
                CarreraOrigen = carreraOrigen,
                AsignaturaOrigenId = 1,
                AsignaturaOrigen = asignaturaOrigen,
                CarreraDestinoId = 2,
                CarreraDestino = carreraDestino,
                AsignaturaDestinoId = idDestinoEquivalencia.Value,
                AsignaturaDestino = idDestinoEquivalencia.Value == 2 ? asignaturaDestino : asignaturaAjena,
                Activa = true
            });
        }

        db.SaveChanges();
        return db;
    }

    private static AsignaturaSolicitudInternaDto ObtenerUnicaAsignatura(SolicitudesDbContext db)
    {
        var controller = new InternoController(db);
        var result = controller.ObtenerCompleta(1).GetAwaiter().GetResult();
        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<SolicitudCompletaInternaDto>(ok.Value);
        return Assert.Single(dto.Asignaturas);
    }

    [Fact]
    public void SinEquivalencia_ElDestinoNoRepiteElNombreOrigen()
    {
        using var db = CrearContexto();
        var asignatura = ObtenerUnicaAsignatura(db);

        Assert.Equal(Origen, asignatura.AsignaturaOrigen);
        Assert.Null(asignatura.AsignaturaDestinoId);
        Assert.Equal(SinEquivalente, asignatura.AsignaturaDestino);
        Assert.NotEqual(asignatura.AsignaturaOrigen, asignatura.AsignaturaDestino);
        Assert.Equal(string.Empty, asignatura.NivelDestino);
        Assert.Equal(string.Empty, asignatura.AreaDestino);
        Assert.Equal(0, asignatura.CreditosDestino);
        Assert.Equal(0, asignatura.HorasDestino);
        Assert.Equal(string.Empty, asignatura.ContenidoDestino);
        Assert.Equal(PlanOrigen, asignatura.ContenidoOrigen);
        Assert.Null(asignatura.EquivalenciaAplicada);
    }

    [Fact]
    public void ConEquivalencia_ContenidoDestinoSaleDelPlanDestino()
    {
        using var db = CrearContexto(idDestinoEquivalencia: 2);
        var asignatura = ObtenerUnicaAsignatura(db);

        Assert.Equal(2, asignatura.AsignaturaDestinoId);
        Assert.Equal(Destino, asignatura.AsignaturaDestino);
        Assert.Equal(PlanDestino, asignatura.ContenidoDestino);
        Assert.DoesNotContain(PlanOrigen, asignatura.ContenidoDestino, StringComparison.Ordinal);
        Assert.Equal(
            $"Equivalencia registrada: {Origen} → {Destino}",
            asignatura.EquivalenciaAplicada);
    }

    [Fact]
    public void EquivalenciaSinDestinoEnLaMalla_UsaElCentinelaTambienEnLaRegla()
    {
        using var db = CrearContexto(idDestinoEquivalencia: 3);
        var asignatura = ObtenerUnicaAsignatura(db);

        Assert.Null(asignatura.AsignaturaDestinoId);
        Assert.Equal(SinEquivalente, asignatura.AsignaturaDestino);
        Assert.Equal(string.Empty, asignatura.ContenidoDestino);
        Assert.Equal(
            $"Equivalencia registrada: {Origen} → {SinEquivalente}",
            asignatura.EquivalenciaAplicada);
    }

    [Fact]
    public void LosDatosInternos_NoConfundenAlComiteConElEstudiante()
    {
        using var db = CrearContexto();
        var controller = new InternoController(db);

        var resultado = controller.ObtenerPorIds("1").GetAwaiter().GetResult();
        var ok = Assert.IsType<OkObjectResult>(resultado);
        var dto = Assert.Single(Assert.IsType<List<SolicitudInternaDto>>(ok.Value));

        Assert.Equal("comite@uta.edu.ec", dto.EstudianteCorreo);
        Assert.Equal("Estudiante Prueba", dto.EstudianteNombre);
        Assert.Equal("1234567890", dto.EstudianteCedula);
    }

    [Fact]
    public async Task ActualizarTitular_GuardaDatosDelRecordSinCambiarElEstado()
    {
        using var db = CrearContexto();
        var controller = new InternoController(db);
        var estadoInicial = db.Solicitudes.Single().Estado;

        var resultado = await controller.ActualizarTitular(1, new ActualizarTitularSolicitudRequest
        {
            Nombre = "María Pérez",
            Cedula = "1800123456",
            Usuario = "coordinador@uta.edu.ec"
        });

        Assert.IsType<OkObjectResult>(resultado);
        var solicitud = await db.Solicitudes.SingleAsync();
        Assert.Equal("María Pérez", solicitud.EstudianteNombre);
        Assert.Equal("1800123456", solicitud.EstudianteCedula);
        Assert.Equal("comite@uta.edu.ec", solicitud.EstudianteCorreo);
        Assert.Equal(estadoInicial, solicitud.Estado);
        Assert.Single(db.HistorialEstados);
    }
}
