using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Validadores;

namespace HomologaUTA.Homologacion.Services;

/// <summary>
/// Ejecuta los validadores de reglas objetivas (RN-01, RN-02, RN-04, RN-05 y RN-06) sobre una solicitud
/// y devuelve el estado de cada asignatura.
/// </summary>
public class ServicioValidacionSolicitud
{
    private readonly ValidadorReglas _validadorReglas;
    private readonly ValidadorDocumentos _validadorDocumentos;

    public ServicioValidacionSolicitud(ValidadorReglas validadorReglas, ValidadorDocumentos validadorDocumentos)
    {
        _validadorReglas = validadorReglas;
        _validadorDocumentos = validadorDocumentos;
    }

    /// <summary>Devuelve un mensaje de error si los datos de entrada no son válidos, o null si lo son.</summary>
    public string? ValidarEntrada(ValidarSolicitudRequest request)
    {
        if (!EsTipoReconocido(request.Tipo))
            return "Tipo de solicitud no reconocido. Use: Cambio de carrera, Cambio de universidad o Movilidad.";

        if (request.Asignaturas.Count == 0 || request.Asignaturas.Any(a => a is null))
            return "Debe enviar al menos una asignatura y ninguna puede estar vacía.";

        if (EsCambioDeUniversidad(request.Tipo) && request.Requisitos is null)
            return "Para un cambio de universidad debe enviar los requisitos del estudiante (RN-05).";

        return null;
    }

    /// <summary>
    /// Orden de decisión por asignatura:
    /// 1) RN-02 o RN-04 propios de la asignatura: EXCLUIDA.
    /// 2) RN-05 no cumplida (solo cambio de universidad): EXCLUIDA.
    /// 3) RN-01 (más de diez años): DERIVADA.
    /// 4) RN-06 no cumplida (documentación incompleta): CON_FALTANTES.
    /// 5) En cualquier otro caso: LISTA_PARA_AGENTE.
    /// </summary>
    public ResultadoValidacionSolicitudDto Validar(int solicitudId, ValidarSolicitudRequest request)
    {
        var resultado = new ResultadoValidacionSolicitudDto
        {
            SolicitudId = solicitudId,
            Tipo = request.Tipo
        };

        // Reglas propias de cada asignatura (RN-02, RN-04 y RN-01, en ese orden)
        var evaluadas = request.Asignaturas
            .Select(a => new
            {
                Dato = a,
                Regla = _validadorReglas.ValidarAsignatura(new AsignaturaInput
                {
                    Nombre = a.Nombre,
                    Aprobada = a.Aprobada ?? false,
                    AniosDesdeAprobacion = a.AniosDesdeAprobacion,
                    NumeroMatricula = a.NumeroMatricula
                })
            })
            .ToList();

        // RN-06: documentación completa
        var documentacion = _validadorDocumentos.ValidarDocumentacion(
            new DocumentacionInput
            {
                TieneMallaOrigen = request.Documentacion.TieneMallaOrigen,
                TieneHistorialAcademico = request.Documentacion.TieneHistorialAcademico,
                TieneSilabos = request.Documentacion.TieneSilabos,
                DocumentosApostillados = request.Documentacion.DocumentosApostillados
            },
            request.EsUniversidadExtranjera);

        resultado.ValidacionesGenerales.Add(new ValidacionGeneralDto
        {
            Regla = documentacion.ReglaAplicada,
            Cumple = documentacion.EsValida,
            Detalle = documentacion.Detalle
        });

        // RN-05: solo en un cambio de universidad. Las "homologables" son las que cumplen las reglas propias.
        ResultadoValidacionDocumentos? requisitos = null;
        if (EsCambioDeUniversidad(request.Tipo) && request.Requisitos is not null)
        {
            requisitos = _validadorDocumentos.ValidarRequisitosCambioUniversidad(new RequisitosCambioUniversidad
            {
                TieneCambiosCarreraPrevios = request.Requisitos.TieneCambiosCarreraPrevios,
                PeriodosCursados = request.Requisitos.PeriodosCursados,
                AsignaturasAprobadasHomologables = evaluadas.Count(e => e.Regla.EsValida)
            });

            resultado.ValidacionesGenerales.Add(new ValidacionGeneralDto
            {
                Regla = requisitos.ReglaAplicada,
                Cumple = requisitos.EsValida,
                Detalle = requisitos.Detalle
            });
        }

        foreach (var e in evaluadas)
        {
            var salida = new ResultadoAsignaturaDto
            {
                Id = e.Dato.Id,
                Asignatura = e.Dato.Nombre
            };

            if (e.Regla.ReglaAplicada is "RN-02" or "RN-04")
            {
                salida.Estado = EstadoValidacion.EXCLUIDA;
                salida.ReglaAplicada = e.Regla.ReglaAplicada;
                salida.Detalle = e.Regla.Detalle;
            }
            else if (requisitos is { EsValida: false })
            {
                salida.Estado = EstadoValidacion.EXCLUIDA;
                salida.ReglaAplicada = requisitos.ReglaAplicada;
                salida.Detalle = requisitos.Detalle;
            }
            else if (e.Regla.ReglaAplicada == "RN-01")
            {
                salida.Estado = EstadoValidacion.DERIVADA;
                salida.ReglaAplicada = e.Regla.ReglaAplicada;
                salida.Detalle = e.Regla.Detalle;
            }
            else if (!documentacion.EsValida)
            {
                salida.Estado = EstadoValidacion.CON_FALTANTES;
                salida.ReglaAplicada = documentacion.ReglaAplicada;
                salida.Detalle = documentacion.Detalle;
            }
            else
            {
                salida.Estado = EstadoValidacion.LISTA_PARA_AGENTE;
                salida.ReglaAplicada = e.Regla.ReglaAplicada;
                salida.Detalle = e.Regla.Detalle;
            }

            resultado.Asignaturas.Add(salida);
        }

        resultado.Resumen = new ResumenValidacionDto
        {
            Total = resultado.Asignaturas.Count,
            ListasParaAgente = resultado.Asignaturas.Count(a => a.Estado == EstadoValidacion.LISTA_PARA_AGENTE),
            Derivadas = resultado.Asignaturas.Count(a => a.Estado == EstadoValidacion.DERIVADA),
            Excluidas = resultado.Asignaturas.Count(a => a.Estado == EstadoValidacion.EXCLUIDA),
            ConFaltantes = resultado.Asignaturas.Count(a => a.Estado == EstadoValidacion.CON_FALTANTES)
        };

        return resultado;
    }

    // "Cambio de universidad" (formulario) y "CAMBIO_UNIVERSIDAD" (contrato) se tratan igual.
    private static string Normalizar(string tipo) => tipo.Trim().ToUpperInvariant().Replace('_', ' ');

    private static bool EsCambioDeUniversidad(string tipo) => Normalizar(tipo).Contains("UNIVERSIDAD");

    private static bool EsTipoReconocido(string tipo)
    {
        var t = Normalizar(tipo);
        return t.Contains("CARRERA") || t.Contains("UNIVERSIDAD") || t.Contains("MOVILIDAD");
    }
}