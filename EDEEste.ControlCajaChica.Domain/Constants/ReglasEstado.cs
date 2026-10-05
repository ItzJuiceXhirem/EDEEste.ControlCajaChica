using System.Collections.Generic;
using EDEEste.ControlCajaChica.Domain.Enums;

namespace EDEEste.ControlCajaChica.Domain.Constants
{
    /// <summary>
    /// Qué estados cuentan para cada regla de negocio, en un solo sitio.
    ///
    /// Las dos reglas listan los estados en POSITIVO, no por exclusión: el día que se
    /// agregue un estado nuevo, queda fuera de la regla hasta que alguien decida a
    /// conciencia que entra (mismo criterio que IGastoRepository.ListarNoRepuestosAsync).
    /// </summary>
    public static class ReglasEstado
    {
        /// <summary>
        /// Un fondo admite gastos y arqueos mientras esté Activo o EnReposicion: que haya
        /// una reposición en camino no frena la operación del Custodio. Un fondo
        /// Inactivo no admite nada.
        /// </summary>
        public static bool FondoAdmiteOperacion(EstadoFondo estado) =>
            estado is EstadoFondo.Activo or EstadoFondo.EnReposicion;

        /// <summary>
        /// Estados en los que una solicitud todavía está viva: ni pagada ni rechazada.
        /// Un fondo puede tener como máximo una solicitud en curso, y esa es la
        /// garantía de que su estado (Activo o EnReposicion) siempre es correcto.
        /// </summary>
        public static readonly IReadOnlyList<EstadoReposicion> ReposicionEnCurso =
        [
            EstadoReposicion.PendienteAprobacion,
            EstadoReposicion.Aprobada,
            EstadoReposicion.DevueltaPorFinanzas
        ];
    }
}
