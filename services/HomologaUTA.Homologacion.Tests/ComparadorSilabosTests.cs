using HomologaUTA.Homologacion.Agents;
using HomologaUTA.Homologacion.Models;
using HomologaUTA.Homologacion.Services;
using Xunit;

namespace HomologaUTA.Homologacion.Tests;

public class ComparadorSilabosTests
{
    private const string BaseDatosOrigen =
        "Objetivo general: Diseñar bases de datos relacionales normalizadas.\n" +
        "U.1 Esquema Conceptual; Introducción a los Sistemas de Bases de Datos; SGBD; Modelo Entidad Relación\n" +
        "U.2 Esquema Lógico; Modelo Relacional\n" +
        "U.3 Normalización; Dependencias Funcionales; Integridad Referencial\n" +
        "U.4 Esquema Físico; Sentencias DDL; Sentencias DML; Sentencias DQL";

    private const string BaseDatosDestino =
        "U.1 Modelo Entidad Relación; Sistemas de gestión de bases de datos\n" +
        "U.2 Modelo relacional; Normalización y dependencias funcionales\n" +
        "U.3 Lenguaje SQL: sentencias DDL y DML";

    private const string AlgebraDestino =
        "U.1 Matrices y determinantes; Sistemas de ecuaciones lineales\n" +
        "U.2 Espacios vectoriales; Transformaciones lineales";

    [Fact]
    public void MismaMateria_CubreLosTemasDelDestino()
    {
        var r = ComparadorSilabos.Comparar(BaseDatosOrigen, null, 144, BaseDatosDestino, null, 144);

        Assert.True(r.Contenido >= 80, $"Contenido {r.Contenido}%");
        Assert.Equal(100, r.Horas);
        Assert.NotEmpty(r.Coincidencias);
    }

    [Fact]
    public void MateriasDistintas_NoAlcanzanElUmbral_YListanDiferencias()
    {
        var r = ComparadorSilabos.Comparar(BaseDatosOrigen, null, 144, AlgebraDestino, null, 144);

        Assert.True(r.Contenido < 30, $"Contenido {r.Contenido}%");
        Assert.NotEmpty(r.Diferencias);
    }

    [Fact]
    public void Horas_SeCalculanConLasMallas_ConTopeDe100()
    {
        Assert.Equal(50, ComparadorSilabos.Comparar(BaseDatosOrigen, null, 72, BaseDatosDestino, null, 144).Horas);
        Assert.Equal(100, ComparadorSilabos.Comparar(BaseDatosOrigen, null, 192, BaseDatosDestino, null, 144).Horas);
    }

    [Fact]
    public void SinDatos_NoSeInventan_SeInformanComoFaltantes()
    {
        var r = ComparadorSilabos.Comparar(null, null, null, BaseDatosDestino, null, null);

        Assert.Null(r.Horas);
        Assert.Equal(0, r.Contenido);
        Assert.Contains(r.DatosFaltantes, f => f.Contains("sílabo"));
        Assert.Contains(r.DatosFaltantes, f => f.Contains("horas"));
    }

    [Theory]
    [InlineData("2012-2013", "APROBADO", true)]
    [InlineData("ABRIL 2022 - SEPTIEMBRE 2022", "APROBADO", false)]
    [InlineData(null, "APROBADO", false)]
    [InlineData("2023", "Aprobada en tercera matrícula", true)]
    public void ReglasDelRecord_DiezAniosYTerceraMatricula(string? periodo, string estado, bool excluida)
    {
        var motivo = ReglasHomologacion.MotivoPorRecord(periodo, estado, anioActual: 2026);
        Assert.Equal(excluida, motivo is not null);
    }

    [Fact]
    public void DepurarCandidatas_ConservaLasQueCumplen_OSoloLaMejor()
    {
        AnalisisAsignatura Fila(string origen, string destino, string decision, int pct) => new()
        {
            AsignaturaOrigen = origen, AsignaturaDestino = destino, Decision = decision,
            PorcentajeContenido = pct, PorcentajeProfundidad = pct, PorcentajeHoras = pct
        };

        var filas = new List<AnalisisAsignatura>
        {
            Fila("Física", "Física", DecisionAnalisis.HOMOLOGABLE, 95),
            Fila("Física", "Cálculo", DecisionAnalisis.NO_HOMOLOGABLE, 10),
            Fila("Redes", "Telemática", DecisionAnalisis.NO_HOMOLOGABLE, 40),
            Fila("Redes", "Antenas", DecisionAnalisis.NO_HOMOLOGABLE, 20),
        };

        var resultado = AnalisisService.DepurarCandidatas(filas);

        Assert.Equal(2, resultado.Count);
        Assert.Contains(resultado, f => f.AsignaturaDestino == "Física");
        Assert.Contains(resultado, f => f.AsignaturaDestino == "Telemática");
    }

    [Theory]
    [InlineData("Cálculo Diferencial", "CALCULO DIFERENCIAL", true)]
    [InlineData("Interacción Hombre Máquina", "Interacción Humano / Computador", false)]
    [InlineData("Lógica Matemática", "Auditoría de TI", false)]
    public void MismoNombre_SoloParaAsignaturasEquivalentesPorNombre(string a, string b, bool esperado) =>
        Assert.Equal(esperado, AnalisisService.MismoNombre(a, b));

    [Fact]
    public void Combinacion_NoReusaUnaMateriaYaHomologadaSola()
    {
        var filas = new List<AnalisisAsignatura>
        {
            new() { AsignaturaOrigen = "Cálculo Diferencial", AsignaturaDestino = "Cálculo Diferencial", Decision = DecisionAnalisis.HOMOLOGABLE },
            new() { AsignaturaOrigen = "Cálculo Integral", AsignaturaDestino = "Cálculo Integral", Decision = DecisionAnalisis.REQUIERE_REVISION },
            new()
            {
                AsignaturaOrigen = "Cálculo Diferencial", AsignaturaDestino = "Cálculo Integral", Decision = DecisionAnalisis.HOMOLOGABLE,
                Combinada = true,
                MateriasOrigen = new() { new() { Nombre = "Cálculo Diferencial" }, new() { Nombre = "Cálculo Integral" } }
            },
        };

        var resultado = AnalisisService.DescartarCombinacionesInnecesarias(filas);

        Assert.Equal(2, resultado.Count);
        Assert.DoesNotContain(resultado, f => f.Combinada);
    }

    [Fact]
    public void PlanGenericoDeDestino_NoDaCoincidenciasFalsas()
    {
        var generico = "Plan analítico de Auditoría de Sistemas de Información (Octavo, 3 créditos, 144 horas). Área: Sistemas. " +
                       "Unidades: fundamentos, conceptos esenciales, aplicaciones y ejercicios de Auditoría de Sistemas de Información.";
        var r = ComparadorSilabos.Comparar("Sistemas de información; auditoría de sistemas; interacción", null, 144, generico, null, 144);
        Assert.Equal(0, r.Contenido);
        Assert.Contains(r.DatosFaltantes, d => d.Contains("contenidos de la asignatura de destino"));
    }

    [Fact]
    public void HorasDeSilabo_LeeElTotalDelSemestre()
    {
        Assert.Equal(144, ComparadorSilabos.HorasDeSilabo("...TOTAL DE HORAS AL SEMESTRE144(Número de horas..."));
        Assert.Null(ComparadorSilabos.HorasDeSilabo("TOTAL DE HORAS AL SEMESTRE 0"));
        Assert.Null(ComparadorSilabos.HorasDeSilabo("sin dato"));
    }
}
