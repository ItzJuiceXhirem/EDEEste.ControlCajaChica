using System.Linq;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;

namespace EDEEste.ControlCajaChica.Application.Features.Reposiciones
{
    /// <summary>
    /// Lo que significa rechazar una solicitud, en un solo sitio. Lo usan las dos
    /// formas de rechazarla: el Gerente (RechazarReposicionHandler) y el cierre de un
    /// fondo por el Administrador (ActualizarParametrosFondoHandler). Con la lógica
    /// compartida, las dos no pueden divergir.
    ///
    /// No toca el estado del fondo: quien rechaza decide qué pasa con él (el Gerente lo
    /// libera si estaba EnReposicion; el cierre lo deja Inactivo). Tampoco mueve dinero:
    /// el efectivo nunca salió del fondo, así que sigue pendiente de reposición.
    /// </summary>
    public static class RechazoDeReposicion
    {
        /// <summary>Motivo con el que se rechaza una solicitud viva al cerrar su fondo.</summary>
        public const string MotivoCierreDeFondo = "Fondo cerrado por el Administrador.";

        public static void Aplicar(SolicitudReposicion solicitud, string motivo)
        {
            solicitud.Estado = EstadoReposicion.Rechazada;
            solicitud.MotivoRechazo = motivo;

          /* Los gastos vuelven al ruedo para que el custodio corrija y arme otra
             solicitud. El balance NO se toca: el efectivo nunca volvió a la caja, se
             sigue debiendo. ToList() no es cosmético: al poner ReposicionId en null, el
             arreglo de relaciones de EF saca el gasto de solicitud.Gastos en plena
             iteración. */
            foreach (var gasto in solicitud.Gastos.ToList())
            {
                gasto.ReposicionId = null;
                gasto.Estado = EstadoGasto.PendienteReposicion;
            }

            // RutaPdfConsolidado se conserva: una rechazada queda en el historial con su expediente.
        }
    }
}
