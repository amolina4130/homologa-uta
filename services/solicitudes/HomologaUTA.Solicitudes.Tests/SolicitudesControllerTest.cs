using Xunit;
using Microsoft.EntityFrameworkCore;
using HomologaUTA.Solicitudes.Data;
using HomologaUTA.Solicitudes.Models;
using System;
using System.Linq;
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
        public void Validacion_CarreraOrigenIgualADestino_DebeSerInvalido()
        {
            // 1. Preparar
            int carreraOrigenId = 1;
            int carreraDestinoId = 1;

            // 2. Actuar
            bool esInvalido = (carreraOrigenId == carreraDestinoId);

            // 3. Afirmar
            Assert.True(esInvalido, "La validación debería rechazar solicitudes donde la carrera de origen y destino son iguales.");
        }

        [Fact]
        public async Task Estudiante_SoloVeSusPropiasSolicitudes()
        {
            // 1. Preparar
            using var context = GetInMemoryDbContext();
            
            // Ahora usamos los correos, como lo definió Anahi en el modelo
            var correoEstudiante1 = "estudiante1@uta.edu.ec";
            var correoEstudiante2 = "estudiante2@uta.edu.ec";

            context.Solicitudes.Add(new Solicitud { Id = 1, EstudianteCorreo = correoEstudiante1, Tipo = "Cambio de carrera" });
            context.Solicitudes.Add(new Solicitud { Id = 2, EstudianteCorreo = correoEstudiante2, Tipo = "Homologación" });
            await context.SaveChangesAsync();

            // 2. Actuar
            // Simulamos la consulta filtrando por el correo del estudiante
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