using System.Collections.Generic;
using EDEEste.ControlCajaChica.Domain.Constants;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;

namespace EDEEste.ControlCajaChica.Application.Features.Reposiciones
{
    /// <summary>
    /// Comprobaciones de una solicitud de reposición y sus gastos, compartidas entre
    /// los handlers que la avanzan de estado (aprobar, rechazar, devolver y pagar):
    /// todos actúan sobre la misma solicitud en distintos puntos de su ciclo de vida y
    /// necesitan la misma garantía -- que sus gastos sigan en EnProcesoReposicion con
    /// la firma de integridad intacta -- antes de moverla.
    /// </summary>
    public static class ValidadorSolicitudReposicion
    {
        public static void ValidarIntegridadSolicitud(List<string> errores, SolicitudReposicion solicitud)
        {
            if (!solicitud.IntegridadVerificada)
            {
                errores.Add("La solicitud tiene la firma de integridad comprometida y no se puede procesar.");
            }
        }

        /// <summary>
        /// Aprobar y rechazar ahora también escriben el fondo (su estado), y el
        /// interceptor de auditoría se niega a guardar una fila con la firma rota: se
        /// avisa aquí, con un mensaje claro, antes de llegar a ese rechazo.
        /// </summary>
        public static void ValidarIntegridadFondo(List<string> errores, FondoCajaChica fondo)
        {
            if (!fondo.IntegridadVerificada)
            {
                errores.Add("El fondo tiene la firma de integridad comprometida.");
            }
        }

        public static void ValidarGastosEnProceso(List<string> errores, SolicitudReposicion solicitud)
        {
            foreach (var gasto in solicitud.Gastos)
            {
                if (gasto.Estado != EstadoGasto.EnProcesoReposicion)
                {
                    errores.Add($"El gasto {gasto.NCF} no está en proceso de reposición; la solicitud está inconsistente.");
                }
            }

            ValidarIntegridadGastos(errores, solicitud);
        }

        /// <summary>
        /// Solo la firma de los gastos, sin exigir su estado. Es lo que necesita el cierre
        /// de un fondo: el Administrador puede cerrarlo en cualquier momento, aunque una
        /// solicitud esté inconsistente, pero el interceptor de auditoría se niega a
        /// guardar un gasto con la firma rota.
        /// </summary>
        public static void ValidarIntegridadGastos(List<string> errores, SolicitudReposicion solicitud)
        {
            foreach (var gasto in solicitud.Gastos)
            {
                if (!gasto.IntegridadVerificada)
                {
                    errores.Add($"El gasto {gasto.NCF} tiene la firma de integridad comprometida.");
                }
            }
        }

        /// <summary>
        /// Un motivo obligatorio: no vacío y dentro del tope de la columna.
        /// <paramref name="queMotiva"/> completa el mensaje ("del rechazo", "de la
        /// devolución", "para aprobarla de nuevo").
        /// </summary>
        public static void ValidarMotivo(List<string> errores, string? motivo, string queMotiva)
        {
            var texto = motivo?.Trim() ?? string.Empty;

            if (texto.Length == 0)
            {
                errores.Add($"Debe indicar el motivo {queMotiva}.");
            }
            else if (texto.Length > LimitesReposicion.LongitudMaximaMotivo)
            {
                errores.Add(
                    $"El motivo {queMotiva} no puede superar {LimitesReposicion.LongitudMaximaMotivo} caracteres.");
            }
        }
    }
}
