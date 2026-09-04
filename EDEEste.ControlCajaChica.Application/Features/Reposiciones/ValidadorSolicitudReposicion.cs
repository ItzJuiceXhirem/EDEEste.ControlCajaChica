using System.Collections.Generic;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;

namespace EDEEste.ControlCajaChica.Application.Features.Reposiciones
{
    /// <summary>
    /// Comprobaciones de integridad de una solicitud de reposicion y sus gastos,
    /// compartidas entre AprobarReposicionHandler y ProcesarPagoReposicionHandler:
    /// ambos actuan sobre la misma solicitud en distintos puntos de su ciclo de vida
    /// y necesitan la misma garantia -- que sus gastos sigan en EnProcesoReposicion
    /// con la firma de integridad intacta -- antes de avanzarla de estado.
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

        public static void ValidarGastosEnProceso(List<string> errores, SolicitudReposicion solicitud)
        {
            foreach (var gasto in solicitud.Gastos)
            {
                if (gasto.Estado != EstadoGasto.EnProcesoReposicion)
                {
                    errores.Add($"El gasto {gasto.NCF} no esta en proceso de reposicion; la solicitud esta inconsistente.");
                }

                if (!gasto.IntegridadVerificada)
                {
                    errores.Add($"El gasto {gasto.NCF} tiene la firma de integridad comprometida.");
                }
            }
        }
    }
}
