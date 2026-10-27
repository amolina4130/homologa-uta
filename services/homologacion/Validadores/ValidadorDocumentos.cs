namespace HomologaUTA.Homologacion.Validadores;

public class DocumentacionInput
{
    public bool TieneMallaOrigen { get; set; }
    public bool TieneHistorialAcademico { get; set; }
    public bool TieneSilabos { get; set; }
    public bool DocumentosApostillados { get; set; } // Solo para universidades extranjeras
}

public class RequisitosCambioUniversidad
{
    public bool TieneCambiosCarreraPrevios { get; set; }
    public int PeriodosCursados { get; set; }
    public int AsignaturasAprobadasHomologables { get; set; }
}

public class ResultadoValidacionDocumentos
{
    public bool EsValida { get; set; }
    public string ReglaAplicada { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
}

public class ValidadorDocumentos
{
    // RN-05: Cambio de universidad (sin cambios de carrera previos, al menos 2 períodos y 2 asignaturas homologables)
    public ResultadoValidacionDocumentos ValidarRequisitosCambioUniversidad(RequisitosCambioUniversidad req)
    {
        var resultado = new ResultadoValidacionDocumentos { EsValida = true, ReglaAplicada = "RN-05" };

        if (req.TieneCambiosCarreraPrevios)
        {
            resultado.EsValida = false;
            resultado.Detalle = "El estudiante tiene cambios de carrera previos. Se excluye según RN-05.";
            return resultado;
        }
        if (req.PeriodosCursados < 2)
        {
            resultado.EsValida = false;
            resultado.Detalle = $"Solo tiene {req.PeriodosCursados} período(s) cursado(s). Se requieren al menos 2 (RN-05).";
            return resultado;
        }
        if (req.AsignaturasAprobadasHomologables < 2)
        {
            resultado.EsValida = false;
            resultado.Detalle = "Tiene menos de 2 asignaturas aprobadas homologables. No cumple RN-05.";
            return resultado;
        }

        resultado.Detalle = "Cumple con los requisitos de cambio de universidad (RN-05).";
        return resultado;
    }

    // RN-06: Documentación completa (en extranjeras deben estar apostillados)
    public ResultadoValidacionDocumentos ValidarDocumentacion(DocumentacionInput docs, bool esUniversidadExtranjera)
    {
        var resultado = new ResultadoValidacionDocumentos { EsValida = true, ReglaAplicada = "RN-06" };
        var faltantes = new List<string>();

        if (!docs.TieneMallaOrigen) faltantes.Add("Malla curricular de origen");
        if (!docs.TieneHistorialAcademico) faltantes.Add("Historial académico");
        if (!docs.TieneSilabos) faltantes.Add("Sílabos de las asignaturas");

        if (esUniversidadExtranjera && !docs.DocumentosApostillados)
        {
            faltantes.Add("Documentos apostillados (obligatorio para universidades extranjeras)");
        }

        if (faltantes.Any())
        {
            resultado.EsValida = false;
            resultado.Detalle = $"Falta la siguiente documentación: {string.Join(", ", faltantes)}.";
            return resultado;
        }

        resultado.Detalle = "La documentación está completa y cumple con la regla RN-06.";
        return resultado;
    }
}