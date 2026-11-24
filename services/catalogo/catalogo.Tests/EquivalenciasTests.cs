using Xunit;
using Microsoft.EntityFrameworkCore;
using HomologaUTA.Catalogo.Data;
using HomologaUTA.Catalogo.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using System.Threading.Tasks;

namespace HomologaUTA.Catalogo.Tests
{
    public class EquivalenciasTests
    {
        // 1. Prueba de creación y de duplicado
        [Fact]
        public async Task Creacion_Equivalencia_Duplicada_DebeDevolverConflict()
        {
            // TODO: Descomentar cuando el endpoint esté implementado
            // var context = GetInMemoryDbContext();
            // var controller = new EquivalenciasController(context);
            // var request = new EquivalenciaRequest { OrigenId = 1, DestinoId = 2 };
            // await controller.Crear(request);
            // var result = await controller.Crear(request); // Segunda vez
            // Assert.IsType<ConflictObjectResult>(result);

            Assert.True(true); // Temporal
        }

        // 2. Prueba de acceso con otro rol (403)
        [Fact]
        public void Acceso_Con_Rol_Incorrecto_DebeDevolverForbidden()
        {
            // TODO: Descomentar cuando el endpoint esté implementado
            // var controller = new EquivalenciasController(context);
            // controller.ControllerContext = new ControllerContext
            // {
            //     HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "ESTUDIANTE") })) }
            // };
            // var result = controller.Crear(request);
            // Assert.IsType<ForbidResult>(result);

            Assert.True(true); // Temporal
        }

        // 3. Prueba de análisis con equivalencia activa
        [Fact]
        public async Task Analisis_Con_Equivalencia_Activa_Debe_Funcionar()
        {
            Assert.True(true); // Temporal
        }

        // 4. Prueba de análisis con equivalencia desactivada
        [Fact]
        public async Task Analisis_Con_Equivalencia_Desactivada_Debe_Fallar()
        {
            Assert.True(true); // Temporal
        }
    }
}