using System;
using System.Collections.Generic;
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
    /// Finanzas devuelve al Gerente una solicitud aprobada, con un motivo obligatorio,
    /// en vez de pagarla. El Gerente la aprueba de nuevo (con su propio motivo) o la
    /// rechaza.
    ///
    /// No toca el fondo ni los gastos: la solicitud sigue viva, así que el fondo sigue
    /// EnReposicion y los gastos siguen en proceso. Por eso tampoco inyecta
    /// IFondoRepository. Comparte permiso con pagar (PagarReposicion): pagar o devolver
    /// es la misma decisión de Finanzas sobre el mismo expediente.
    /// </summary>
    public sealed class DevolverReposicionHandler
    {
        private readonly IReposicionRepository _reposiciones;
        private readonly IAutorizacionService _autorizacion;
        private readonly IApplicationDbContext _contexto;

        public DevolverReposicionHandler(
            IReposicionRepository reposiciones,
            IAutorizacionService autorizacion,
            IApplicationDbContext contexto)
        {
            _reposiciones = reposiciones;
            _autorizacion = autorizacion;
            _contexto = contexto;
        }

        public async Task<ResultadoOperacion<Guid>> EjecutarAsync(
            DevolverReposicionCommand comando,
            CancellationToken cancellationToken = default)
        {
            if (!await _autorizacion.TienePermisoAsync(Permisos.PagarReposicion, cancellationToken))
            {
                return ResultadoOperacion<Guid>.Fallo("No tiene permiso para devolver una reposición.");
            }

            var solicitud = await _reposiciones.ObtenerConDetalleAsync(comando.ReposicionId, cancellationToken);
            if (solicitud is null)
            {
                return ResultadoOperacion<Guid>.Fallo("La solicitud indicada no existe.");
            }

            var errores = Validar(comando, solicitud);
            if (errores.Count > 0)
            {
                return ResultadoOperacion<Guid>.Fallo(errores);
            }

            /* FinanzasUsuarioId no se escribe aquí: significa "quién pagó". Quién devolvió
               queda en la bitácora de auditoría, que registra al usuario de cada guardado. */
            solicitud.Estado = EstadoReposicion.DevueltaPorFinanzas;
            solicitud.MotivoDevolucion = comando.Motivo.Trim();

            if (!await _contexto.IntentarGuardarCambiosAsync(cancellationToken))
            {
                return ResultadoOperacion<Guid>.Fallo(
                    "Otro usuario modificó esta solicitud mientras usted trabajaba. Recargue la pantalla e intente de nuevo.");
            }

            return ResultadoOperacion<Guid>.Ok(solicitud.Id);
        }

        private static List<string> Validar(DevolverReposicionCommand comando, SolicitudReposicion solicitud)
        {
            var errores = new List<string>();

            if (solicitud.Estado != EstadoReposicion.Aprobada)
            {
                errores.Add(
                    $"Solo se puede devolver una solicitud aprobada. Esta solicitud está en estado {solicitud.Estado}.");
            }

            ValidadorSolicitudReposicion.ValidarMotivo(errores, comando.Motivo, "de la devolución");
            ValidadorSolicitudReposicion.ValidarIntegridadSolicitud(errores, solicitud);
            ValidadorSolicitudReposicion.ValidarGastosEnProceso(errores, solicitud);

            return errores;
        }
    }
}
