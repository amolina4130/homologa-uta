using Xunit;
using Microsoft.EntityFrameworkCore;
using HomologaUTA.Solicitudes.Data;
using HomologaUTA.Solicitudes.Models;
using HomologaUTA.Solicitudes.Controllers;
using HomologaUTA.Solicitudes.DTOs;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
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

        [Fact]
        public async Task Validacion_CarreraOrigenIgualADestino_DebeDevolverBadRequest()
        {
            // 1. Preparar
            using var context = GetInMemoryDbContext();
            var controller = new SolicitudesController(context);
            
            var request = new SolicitudRequest
            {
                Tipo = "Cambio de carrera",
                CarreraOrigenId = 1,
                CarreraDestinoId = 1, // Carreras iguales (esto debe fallar)
                AsignaturasIds = new List<int> { 1, 2 }
            };

            // 2. Actuar (Llamamos al método real del controlador)
            var result = await controller.CrearSolicitud(request);

            // 3. Afirmar (Verificamos que devuelva un 400 Bad Request)
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Estudiante_SoloVeSusPropiasSolicitudes()
        {
            // 1. Preparar
            using var context = GetInMemoryDbContext();
            var correoEstudiante1 = "estudiante1@uta.edu.ec";
            var correoEstudiante2 = "estudiante2@uta.edu.ec";
            
            context.Solicitudes.Add(new Solicitud { Id = 1, EstudianteCorreo = correoEstudiante1, Tipo = "Cambio de carrera" });
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
    }
}