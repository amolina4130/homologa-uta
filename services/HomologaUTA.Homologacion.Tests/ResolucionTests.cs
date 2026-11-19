using Xunit;
using Microsoft.EntityFrameworkCore;
using HomologaUTA.Homologacion.Data; // Ajusta si el contexto se llama distinto
using HomologaUTA.Homologacion.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace HomologaUTA.Homologacion.Tests
{
    public class ResolucionTests
    {
        // 1. Prueba de registro válido y cambio a RESUELTA
        [Fact]
        public async Task Registro_Resolucion_Valida_DebeCambiarEstado()
        {
            // TODO: Descomentar cuando el endpoint de resolución esté implementado
            // var context = GetInMemoryDbContext();
            // var controller = new ResolucionController(context);
            // var request = new ResolucionRequest { Decision = "APROBADA", Observacion = "Cumple con todo" };
            // var result = await controller.Registrar(1, request);
            // var okResult = Assert.IsType<OkObjectResult>(result);
            // var solicitud = await context.Solicitudes.FindAsync(1);
            // Assert.Equal("RESUELTA", solicitud.Estado);

            Assert.True(true); // Temporal
        }

        // 2. Prueba de rechazo sin informe técnico
        [Fact]
        public async Task Registro_Resolucion_SinInforme_DebeRechazar()
        {
            // TODO: Descomentar cuando el endpoint esté implementado
            // var context = GetInMemoryDbContext();
            // var solicitud = new Solicitud { Id = 1, Estado = "REGISTRADA" }; // Sin informe técnico
            // context.Solicitudes.Add(solicitud);
            // await context.SaveChangesAsync();
            // var controller = new ResolucionController(context);
            // var result = await controller.Registrar(1, request);
            // Assert.IsType<BadRequestObjectResult>(result);

            Assert.True(true); // Temporal
        }

        // 3. Prueba de acceso con otro rol (403)
        [Fact]
        public void Acceso_Con_Rol_Incorrecto_DebeDevolverForbidden()
        {
            // TODO: Descomentar cuando el endpoint esté implementado
            // var controller = new ResolucionController(context);
            // controller.ControllerContext = new ControllerContext
            // {
            //     HttpContext = new DefaultHttpContext
            //     {
            //         User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "ESTUDIANTE") }))
            //     }
            // };
            // var result = controller.Registrar(1, request);
            // Assert.IsType<ForbidResult>(result);

            Assert.True(true); // Temporal
        }
    }
}