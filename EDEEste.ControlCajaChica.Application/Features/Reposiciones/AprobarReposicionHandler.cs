using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Domain.Constants;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;

namespace EDEEste.ControlCajaChica.Application.Features.Reposiciones
{
    /// <summary>
    /// Aprueba o rechaza una solicitud de reposicion.
    ///
    /// No inyecta IFondoRepository a proposito: ni aprobar ni rechazar mueven dinero,
    /// y no tener el repositorio disponible lo hace evidente en la firma del
    /// constructor. El efectivo solo vuelve al fondo cuando Finanzas paga
    /// (ProcesarPagoReposicionHandler).
    /// </summary>
    public sealed class AprobarReposicionHandler
    {
        private readonly IReposicionRepository _reposiciones;
        private readonly ICurrentUserService _usuarioActual;
        private readonly IAutorizacionService _autorizacion;
        private readonly IApplicationDbContext _contexto;

        public AprobarReposicionHandler(
            IReposicionRepository reposiciones,
            ICurrentUserService usuarioActual,
            IAutorizacionService autorizacion,
            IApplicationDbContext contexto)
        {
            _reposiciones = reposiciones;
            _usuarioActual = usuarioActual;
            _autorizacion = autorizacion;
            _contexto = contexto;
        }

        public async Task<ResultadoOperacion<Guid>> EjecutarAsync(
            AprobarReposicionCommand comando,
            CancellationToken cancellationToken = default)
        {
            if (!await _autorizacion.TienePermisoAsync(Permisos.AprobarReposicion, cancellationToken))
            {
                return ResultadoOperacion<Guid>.Fallo("No tiene permiso para aprobar o rechazar una reposición.");
            }

            var solicitud = await _reposiciones.ObtenerConDetalleAsync(comando.ReposicionId, cancellationToken);
            if (solicitud is null)
            {
                return ResultadoOperacion<Guid>.Fallo("La solicitud indicada no existe.");
            }

            var errores = Validar(solicitud);
            if (errores.Count > 0)
            {
                return ResultadoOperacion<Guid>.Fallo(errores);
            }

            var usuario = await _usuarioActual.ObtenerAsync(cancellationToken);

            solicitud.GerenteUsuarioId = usuario.Id ?? string.Empty;
            solicitud.FechaAprobacion = DateTime.UtcNow;

            if (comando.Aprobar)
            {
                solicitud.Estado = EstadoReposicion.Aprobada;
            }
            else
            {
                solicitud.Estado = EstadoReposicion.Rechazada;

                // Los gastos vuelven al ruedo para que el custodio corrija y arme otra
                // solicitud. El balance NO se toca: el efectivo nunca volvio a la
                // caja, se sigue debiendo. ToList() no es cosmetico: al poner
                // ReposicionId en null, el arreglo de relaciones de EF saca el gasto
                // de solicitud.Gastos en plena iteracion.
                foreach (var gasto in solicitud.Gastos.ToList())
                {
                    gasto.ReposicionId = null;
                    gasto.Estado = EstadoGasto.PendienteReposicion;
                }
            }

            // RutaPdfConsolidado se conserva: una rechazada queda en el historial con
            // su expediente.
            if (!await _contexto.IntentarGuardarCambiosAsync(cancellationToken))
            {
                return ResultadoOperacion<Guid>.Fallo(
                    "Otro usuario modifico esta solicitud mientras usted trabajaba. Recargue la pantalla e intente de nuevo.");
            }

            return ResultadoOperacion<Guid>.Ok(solicitud.Id);
        }

        private static List<string> Validar(SolicitudReposicion solicitud)
        {
            var errores = new List<string>();

            if (solicitud.Estado != EstadoReposicion.PendienteAprobacion)
            {
                errores.Add(
                    $"Solo se puede aprobar o rechazar una solicitud pendiente de aprobacion. " +
                    $"Esta solicitud esta en estado {solicitud.Estado}.");
            }

            ValidadorSolicitudReposicion.ValidarIntegridadSolicitud(errores, solicitud);
            ValidadorSolicitudReposicion.ValidarGastosEnProceso(errores, solicitud);

            return errores;
        }
    }
}
