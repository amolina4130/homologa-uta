using HomologaUTA.Homologacion.Services;

namespace HomologaUTA.Homologacion.Tests;

/// <summary>
/// Pruebas del clasificador por área y de la conversión de nivel textual a número (HU-19/HU-21).
/// </summary>
public class AreaAsignaturaTests
{
    [Theory]
    [InlineData("Cálculo Diferencial", "Matemáticas")]
    [InlineData("Álgebra Lineal", "Matemáticas")]
    [InlineData("Estadística Aplicada", "Matemáticas")]
    [InlineData("Física II", "Física")]
    [InlineData("Química Orgánica", "Química")]
    [InlineData("Programación Orientada a Objetos", "Programación")]
    [InlineData("Estructura de Datos", "Programación")]
    [InlineData("Electrónica Analógica", "Electrónica")]
    [InlineData("Procesos Industriales", "Industrial")]
    [InlineData("Administración de Empresas", "Administración")]
    [InlineData("Metodología de la Investigación", "Humanística")]
    public void Detectar_ClasificaAsignaturasConocidas(string nombre, string areaEsperada)
    {
        Assert.Equal(areaEsperada, AreaAsignatura.Detectar(nombre));
    }

    [Fact]
    public void Detectar_SinClavesConocidas_DevuelveGeneral()
    {
        Assert.Equal("General", AreaAsignatura.Detectar("Taller de expresión corporal"));
    }

    [Theory]
    [InlineData("Primero", 1)]
    [InlineData("Segundo", 2)]
    [InlineData("Tercero", 3)]
    [InlineData("Cuarto", 4)]
    [InlineData("Quinto", 5)]
    [InlineData("Sexto", 6)]
    [InlineData("Séptimo", 7)]
    [InlineData("Octavo", 8)]
    [InlineData("Noveno", 9)]
    [InlineData("Décimo", 10)]
    [InlineData("Nivel 5", 5)]
    [InlineData("4", 4)]
    public void NivelNumerico_ConvierteNivelesTextualesYNumericos(string nivel, int esperado)
    {
        Assert.Equal(esperado, AreaAsignatura.NivelNumerico(nivel));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NivelNumerico_SinNivel_DevuelveUno(string? nivel)
    {
        Assert.Equal(1, AreaAsignatura.NivelNumerico(nivel));
    }
}
