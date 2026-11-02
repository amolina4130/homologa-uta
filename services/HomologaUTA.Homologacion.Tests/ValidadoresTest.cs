using Xunit;
using HomologaUTA.Homologacion.Validadores;

namespace HomologaUTA.Homologacion.Tests
{
    public class ValidadoresTests
    {
        private readonly ValidadorReglas _validadorReglas;
        private readonly ValidadorDocumentos _validadorDocs;

        public ValidadoresTests()
        {
            _validadorReglas = new ValidadorReglas();
            _validadorDocs = new ValidadorDocumentos();
        }

        // ==========================================
        // PRUEBAS PARA EL VALIDADOR DE REGLAS (RN-01, RN-02, RN-04)
        // ==========================================

        [Fact]
        public void RN02_AsignaturaNoAprobada_DebeSerInvalida()
        {
            // Arrange
            var input = new AsignaturaInput { Nombre = "Cálculo", Aprobada = false, AniosDesdeAprobacion = 2, NumeroMatricula = 1 };

            // Act
            var resultado = _validadorReglas.ValidarAsignatura(input);

            // Assert
            Assert.False(resultado.EsValida);
            Assert.Equal("RN-02", resultado.ReglaAplicada);
        }

        [Fact]
        public void RN04_TerceraMatricula_DebeSerInvalida()
        {
            // Arrange
            var input = new AsignaturaInput { Nombre = "Física", Aprobada = true, NumeroMatricula = 3 };

            // Act
            var resultado = _validadorReglas.ValidarAsignatura(input);

            // Assert
            Assert.False(resultado.EsValida);
            Assert.Equal("RN-04", resultado.ReglaAplicada);
        }

        [Fact]
        public void RN01_MasDe10Anios_DebeSerInvalida()
        {
            // Arrange
            var input = new AsignaturaInput { Nombre = "Química", Aprobada = true, AniosDesdeAprobacion = 11, NumeroMatricula = 1 };

            // Act
            var resultado = _validadorReglas.ValidarAsignatura(input);

            // Assert
            Assert.False(resultado.EsValida);
            Assert.Equal("RN-01", resultado.ReglaAplicada);
        }

        [Fact]
        public void CasoExito_AsignaturaCumpleTodo_DebeSerValidaParaElAgente()
        {
            // Arrange: Asignatura aprobada, hace 5 años, en primera matrícula
            var input = new AsignaturaInput { Nombre = "Programación", Aprobada = true, AniosDesdeAprobacion = 5, NumeroMatricula = 1 };

            // Act
            var resultado = _validadorReglas.ValidarAsignatura(input);

            // Assert
            Assert.True(resultado.EsValida);
            Assert.Equal("Ninguna", resultado.ReglaAplicada);
            Assert.Contains("Lista para el agente de IA", resultado.Detalle);
        }

        // ==========================================
        // PRUEBAS PARA EL VALIDADOR DE DOCUMENTOS (RN-05, RN-06)
        // ==========================================

        [Fact]
        public void RN05_CambioUniversidadConCambiosPrevios_DebeSerInvalido()
        {
            // Arrange
            var req = new RequisitosCambioUniversidad { TieneCambiosCarreraPrevios = true, PeriodosCursados = 3, AsignaturasAprobadasHomologables = 2 };

            // Act
            var resultado = _validadorDocs.ValidarRequisitosCambioUniversidad(req);

            // Assert
            Assert.False(resultado.EsValida);
            Assert.Equal("RN-05", resultado.ReglaAplicada);
        }

        [Fact]
        public void RN05_MenosDeDosPeriodos_DebeSerInvalido()
        {
            // Arrange
            var req = new RequisitosCambioUniversidad { TieneCambiosCarreraPrevios = false, PeriodosCursados = 1, AsignaturasAprobadasHomologables = 2 };

            // Act
            var resultado = _validadorDocs.ValidarRequisitosCambioUniversidad(req);

            // Assert
            Assert.False(resultado.EsValida);
            Assert.Equal("RN-05", resultado.ReglaAplicada);
        }

        [Fact]
        public void RN06_FaltaDocumentacion_DebeSerInvalidoYDecirQueFalta()
        {
            // Arrange
            var docs = new DocumentacionInput { TieneMallaOrigen = true, TieneHistorialAcademico = false, TieneSilabos = true };
            
            // Act
            var resultado = _validadorDocs.ValidarDocumentacion(docs, esUniversidadExtranjera: false);

            // Assert
            Assert.False(resultado.EsValida);
            Assert.Equal("RN-06", resultado.ReglaAplicada);
            Assert.Contains("Historial académico", resultado.Detalle);
        }

        [Fact]
        public void RN06_UniversidadExtranjeraSinApostillar_DebeSerInvalido()
        {
            // Arrange
            var docs = new DocumentacionInput { TieneMallaOrigen = true, TieneHistorialAcademico = true, TieneSilabos = true, DocumentosApostillados = false };
            
            // Act
            var resultado = _validadorDocs.ValidarDocumentacion(docs, esUniversidadExtranjera: true);

            // Assert
            Assert.False(resultado.EsValida);
            Assert.Equal("RN-06", resultado.ReglaAplicada);
            Assert.Contains("apostillados", resultado.Detalle);
        }

        [Fact]
        public void CasoExito_DocumentacionCompletaYDocsApostillados_DebeSerValida()
        {
            // Arrange
            var docs = new DocumentacionInput { TieneMallaOrigen = true, TieneHistorialAcademico = true, TieneSilabos = true, DocumentosApostillados = true };
            
            // Act
            var resultado = _validadorDocs.ValidarDocumentacion(docs, esUniversidadExtranjera: true);

            // Assert
            Assert.True(resultado.EsValida);
            Assert.Contains("está completa", resultado.Detalle);
        }
    }
}