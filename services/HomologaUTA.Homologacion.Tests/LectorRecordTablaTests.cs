using HomologaUTA.Homologacion.Agents;
using Xunit;

namespace HomologaUTA.Homologacion.Tests;

public class LectorRecordTablaTests
{
    // Texto tal como lo entrega el servicio de documentos (columnas pegadas, sin espacios).
    private const string RecordUta =
        "UNIVERSIDAD TÉCNICA DE AMBATORÉCORD ACADÉMICOEstudianteKevin Santiago Toapanta LascanoCédula (ficticia)1799999997" +
        "CarreraTecnologías de la InformaciónNivel 1 — Oct 2024 – Mar 2025CódigoAsignaturaCréditos(sim.)Nota finalAsistencia%Estado" +
        "08060112Álgebra Lineal38.593Aprobada08060114Cálculo Diferencial37.889Aprobada" +
        "Nivel 3 — Oct 2025 – Mar 2026CódigoAsignaturaCréditos(sim.)Nota finalAsistencia%Estado" +
        "08060316Fundamentos de Redes y Comunicación deDatos38.392Aprobada08060313Probabilidad y Estadística34.580Reprobada" +
        "08060312Programación Avanzada39.75Aprobada";

    [Fact]
    public void LeeMateriasNotasEstadosYPeriodosDelFormatoUta()
    {
        var r = LectorRecordTabla.Leer(RecordUta);

        Assert.Equal(5, r.Materias.Count);
        Assert.Equal("Kevin Santiago Toapanta Lascano", r.Nombre);
        Assert.Equal("1799999997", r.Cedula);

        var algebra = r.Materias[0];
        Assert.Equal("Álgebra Lineal", algebra.Nombre);
        Assert.Equal(8.5m, algebra.Nota);
        Assert.Equal("aprobada", algebra.EstadoSegunDocumento);
        Assert.Equal("Oct 2024 – Mar 2025", algebra.Periodo);

        Assert.Equal("Fundamentos de Redes y Comunicación de Datos", r.Materias[2].Nombre);
        Assert.Equal("Oct 2025 – Mar 2026", r.Materias[2].Periodo);
        Assert.Equal("reprobada", r.Materias[3].EstadoSegunDocumento);
        Assert.Equal(4.5m, r.Materias[3].Nota);
        Assert.Equal(9.75m, r.Materias[4].Nota); // sin asistencia y con dos decimales
    }

    [Fact]
    public void OtroFormatoDevuelveVacioParaUsarLaIA()
    {
        var r = LectorRecordTabla.Leer("Materia: Física, nota nueve, semestre primero. Materia: Química, nota ocho.");
        Assert.Empty(r.Materias);
    }
}
