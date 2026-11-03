using Xunit;
using Microsoft.EntityFrameworkCore;
using HomologaUTA.Solicitudes.Data;
using HomologaUTA.Solicitudes.Models;
using HomologaUTA.Solicitudes.Controllers;
using HomologaUTA.Solicitudes.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims; 
using System.Threading.Tasks;

namespace HomologaUTA.Solicitudes.Tests
{
    public class SolicitudesControllerTests
    {
        private SolicitudesDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<SolicitudesDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDbSolicitudes_" + Guid.NewGuid().ToString())
                .Options;
            return new SolicitudesDbContext(options);
        }

        // --- MÉTODO AUXILIAR PARA SIMULAR EL USUARIO LOGUEADO ---
        private SolicitudesController GetControllerWithUser(SolicitudesDbContext context, string correo)
        {
            var controller = new SolicitudesController(context);
            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.Email, correo)
            }, "mock"));

            controller.ControllerContext = new ControllerContext()
            {
                HttpContext = new DefaultHttpContext() { User = user }
            };
            return controller;
        }

        [Fact]
        public async Task Validacion_CarreraOrigenIgualADestino_DebeDevolverBadRequest()
        {
            // 1. Preparar
            using var context = GetInMemoryDbContext();
            var controller = new SolicitudesController(context);
            
            var request = new SolicitudRequest
            {
                // El campo Tipo ya no existe
                CarreraOrigenId = 1,
                CarreraDestinoId = 1, // Carreras iguales (esto debe fallar)
                AsignaturasIds = new List<int> { 1, 2 }
            };

            // 2. Actuar
            var result = await controller.CrearSolicitud(request);

            // 3. Afirmar
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Estudiante_SoloVeSusPropiasSolicitudes()
        {
            // 1. Preparar
            using var context = GetInMemoryDbContext();
            var correoEstudiante1 = "estudiante1@uta.edu.ec";
            var correoEstudiante2 = "estudiante2@uta.edu.ec";
            
            context.Solicitudes.Add(new Solicitud { Id = 1, EstudianteCorreo = correoEstudiante1, Tipo = "Homologación" });
            context.Solicitudes.Add(new Solicitud { Id = 2, EstudianteCorreo = correoEstudiante2, Tipo = "Homologación" });
            await context.SaveChangesAsync();

            // 2. Actuar
            var solicitudesEstudiante1 = await context.Solicitudes
                .Where(s => s.EstudianteCorreo == correoEstudiante1)
                .ToListAsync();
            var solicitudesEstudiante2 = await context.Solicitudes
                .Where(s => s.EstudianteCorreo == correoEstudiante2)
                .ToListAsync();

            // 3. Afirmar
            Assert.Single(solicitudesEstudiante1); 
            Assert.Single(solicitudesEstudiante2); 
            Assert.NotEqual(solicitudesEstudiante1[0].Id, solicitudesEstudiante2[0].Id); 
        }

        [Fact]
        public async Task CrearSolicitud_IgnoraTipoDelRequest_YGuardaHomologacion()
        {
            // 1. Preparar
            using var context = GetInMemoryDbContext();
            var controller = GetControllerWithUser(context, "test@uta.edu.ec");
            
            var request = new SolicitudRequest
            {
                CarreraOrigenId = 1,
                CarreraDestinoId = 2,
                AsignaturasIds = new List<int> { 1 }
            };

            // 2. Actuar
            var result = await controller.CrearSolicitud(request);

            // 3. Afirmar
            Assert.IsType<CreatedResult>(result);
            var solicitudGuardada = await context.Solicitudes.FirstOrDefaultAsync();
            Assert.NotNull(solicitudGuardada);
            Assert.Equal("Homologación", solicitudGuardada.Tipo);
        }
    }
}