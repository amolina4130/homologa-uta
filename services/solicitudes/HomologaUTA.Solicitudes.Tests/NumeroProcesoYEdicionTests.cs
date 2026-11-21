using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HomologaUTA.Solicitudes.Controllers;
using HomologaUTA.Solicitudes.Data;
using HomologaUTA.Solicitudes.DTOs;
using HomologaUTA.Solicitudes.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomologaUTA.Solicitudes.Tests;

public class NumeroProcesoYEdicionTests
{
    private const string Ana = "ana@uta.edu.ec";
    private const string Luis = "luis@uta.edu.ec";

    private static SolicitudesDbContext NuevoContexto()
    {
        var opciones = new DbContextOptionsBuilder<SolicitudesDbContext>()
            .UseInMemoryDatabase("TestEdicion_" + Guid.NewGuid())
            .Options;
        return new SolicitudesDbContext(opciones);
    }

    // Controlador con el correo en el contenido del token (como llega desde el Gateway).
    private static SolicitudesController Controlador(SolicitudesDbContext db, string correo)
    {
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"email\":\"" + correo + "\"}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var contexto = new DefaultHttpContext();
        contexto.Request.Headers.Authorization = $"Bearer e30.{payload}.firma";

        return new SolicitudesController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = contexto }
        };
    }

    private static SolicitudRequest Pedido(params int[] asignaturas) => new()
    {
        CarreraOrigenId = 1,
        CarreraDestinoId = 2,
        AsignaturasIds = asignaturas.ToList()
    };

    [Fact]
    public async Task Crear_AsignaNumeroDeProcesoLegibleYUnico()
    {
        using var db = NuevoContexto();
        var controlador = Controlador(db, Ana);

        var resultado = await controlador.CrearSolicitud(Pedido(1));
        await controlador.CrearSolicitud(Pedido(2));

        var creada = Assert.IsType<CreatedResult>(resultado);
        var enRespuesta = creada.Value!.GetType().GetProperty("numeroProceso")!.GetValue(creada.Value) as string;
        Assert.StartsWith("SOL-", enRespuesta);

        var guardadas = await db.Solicitudes.OrderBy(s => s.Id).ToListAsync();
        Assert.Equal(2, guardadas.Count);
        Assert.Equal($"SOL-{guardadas[0].Id:D4}", guardadas[0].NumeroProceso);
        Assert.Equal(2, guardadas.Select(s => s.NumeroProceso).Distinct().Count());
    }

    [Fact]
    public async Task Crear_TomaElCorreoDelContenidoDelToken()
    {
        using var db = NuevoContexto();
        await Controlador(db, "maria@uta.edu.ec").CrearSolicitud(Pedido(1));

        Assert.Equal("maria@uta.edu.ec", (await db.Solicitudes.FirstAsync()).EstudianteCorreo);
    }

    [Fact]
    public async Task Editar_SolicitudRegistrada_ActualizaDatosYRegistraHistorial()
    {
        using var db = NuevoContexto();
        await Controlador(db, Ana).CrearSolicitud(Pedido(1));
        var id = (await db.Solicitudes.FirstAsync()).Id;

        var edicion = new SolicitudRequest
        {
            CarreraOrigenId = 3,
            CarreraDestinoId = 4,
            AsignaturasIds = new List<int> { 5, 6 },
            Modulo = "EXTERNO",
            UniversidadOrigen = "Universidad Central",
            UniversidadDestino = "Universidad Tecnica"
        };

        var resultado = await Controlador(db, Ana).EditarSolicitud(id, edicion);

        Assert.IsType<OkObjectResult>(resultado);
        var guardada = await db.Solicitudes.Include(s => s.Asignaturas).FirstAsync(s => s.Id == id);
        Assert.Equal(3, guardada.CarreraOrigenId);
        Assert.Equal(4, guardada.CarreraDestinoId);
        Assert.Equal("EXTERNO", guardada.Modulo);
        Assert.Equal("Universidad Central", guardada.UniversidadOrigen);
        Assert.Equal(new[] { 5, 6 }, guardada.Asignaturas.Select(a => a.AsignaturaId).OrderBy(x => x).ToArray());

        var historial = await db.HistorialEstados.Where(h => h.SolicitudId == id).OrderBy(h => h.Id).ToListAsync();
        Assert.Equal(2, historial.Count);
        var ultimo = historial.Last();
        Assert.Equal(Ana, ultimo.UsuarioCorreo);
        Assert.Equal(EstadoSolicitud.REGISTRADA, ultimo.EstadoAnterior);
        Assert.Equal(EstadoSolicitud.REGISTRADA, ultimo.EstadoNuevo);
        Assert.Contains("editada", ultimo.Detalle);
    }

    [Fact]
    public async Task Editar_SolicitudDeOtroEstudiante_Devuelve403SinCambios()
    {
        using var db = NuevoContexto();
        await Controlador(db, Ana).CrearSolicitud(Pedido(1));
        var id = (await db.Solicitudes.FirstAsync()).Id;

        var resultado = await Controlador(db, Luis).EditarSolicitud(id, Pedido(2));

        var objeto = Assert.IsType<ObjectResult>(resultado);
        Assert.Equal(403, objeto.StatusCode);
        var guardada = await db.Solicitudes.Include(s => s.Asignaturas).FirstAsync();
        Assert.Equal(new[] { 1 }, guardada.Asignaturas.Select(a => a.AsignaturaId).ToArray());
        Assert.Single(db.HistorialEstados);
    }

    [Theory]
    [InlineData("PENDIENTE_REVISION")]
    [InlineData("INFORME_EMITIDO")]
    [InlineData("APROBADA")]
    [InlineData("RECHAZADA")]
    public async Task Editar_FueraDeRegistrada_Devuelve409(string estado)
    {
        using var db = NuevoContexto();
        db.Solicitudes.Add(new Solicitud
        {
            Id = 1,
            Tipo = "Homologación",
            NumeroProceso = "SOL-0001",
            EstudianteCorreo = Ana,
            Estado = estado,
            CarreraOrigenId = 1,
            CarreraDestinoId = 2
        });
        await db.SaveChangesAsync();

        var resultado = await Controlador(db, Ana).EditarSolicitud(1, Pedido(9));

        Assert.IsType<ConflictObjectResult>(resultado);
        Assert.Empty(db.HistorialEstados);
        Assert.Equal(estado, (await db.Solicitudes.FirstAsync()).Estado);
    }

    [Fact]
    public async Task Editar_SolicitudInexistente_Devuelve404()
    {
        using var db = NuevoContexto();

        var resultado = await Controlador(db, Ana).EditarSolicitud(99, Pedido(1));

        Assert.IsType<NotFoundObjectResult>(resultado);
    }

    [Fact]
    public async Task Editar_ConCarrerasIguales_Devuelve400SinCambios()
    {
        using var db = NuevoContexto();
        await Controlador(db, Ana).CrearSolicitud(Pedido(1));
        var id = (await db.Solicitudes.FirstAsync()).Id;

        var invalida = new SolicitudRequest { CarreraOrigenId = 2, CarreraDestinoId = 2, AsignaturasIds = new List<int> { 1 } };
        var resultado = await Controlador(db, Ana).EditarSolicitud(id, invalida);

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Equal(1, (await db.Solicitudes.FirstAsync()).CarreraOrigenId);
        Assert.Single(db.HistorialEstados);
    }
}
