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
    /// Rechaza una solicitud de reposición, siempre con un motivo que el Custodio puede
    /// leer para corregir y volver a solicitar:
    ///  - pendiente de aprobación: el Gerente escribe el motivo;
    ///  - devuelta por Finanzas: el motivo es el que Finanzas ya dio, así el Gerente no
    ///    inventa uno distinto al que originó la devolución.
    ///
    /// Si el fondo estaba EnReposicion (solo ocurre cuando la solicitud venía devuelta)
    /// vuelve a Activo. Rechazar no mueve dinero (el efectivo nunca salió del fondo),
    /// por eso comparte permiso con aprobar y tampoco inyecta IFondoRepository.
    /// </summary>
    public sealed class RechazarReposicionHandler
    {
        private readonly IReposicionRepository _reposiciones;
        private readonly ICurrentUserService _usuarioActual;
        private readonly IAutorizacionService _autorizacion;
        private readonly IApplicationDbContext _contexto;

        public RechazarReposicionHandler(
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
            RechazarReposicionCommand comando,
            CancellationToken cancellationToken = default)
        {
            if (!await _autorizacion.TienePermisoAsync(Permisos.AprobarReposicion, cancellationToken))
            {
                return ResultadoOperacion<Guid>.Fallo("No tiene permiso para rechazar una reposición.");
            }

            var solicitud = await _reposiciones.ObtenerConDetalleAsync(comando.ReposicionId, cancellationToken);
            if (solicitud is null)
            {
                return ResultadoOperacion<Guid>.Fallo("La solicitud indicada no existe.");
            }

            if (solicitud.FondoCajaChica is null)
            {
                return ResultadoOperacion<Guid>.Fallo("El fondo de la solicitud no existe.");
            }

            var fondo = solicitud.FondoCajaChica;

            var motivo = solicitud.Estado == EstadoReposicion.DevueltaPorFinanzas
                ? solicitud.MotivoDevolucion
                : comando.Motivo;

            var errores = Validar(solicitud, fondo, motivo);
            if (errores.Count > 0)
            {
                return ResultadoOperacion<Guid>.Fallo(errores);
            }

            var usuario = await _usuarioActual.ObtenerAsync(cancellationToken);

            solicitud.GerenteUsuarioId = usuario.Id ?? string.Empty;
            solicitud.FechaAprobacion = DateTime.UtcNow;
            RechazoDeReposicion.Aplicar(solicitud, motivo!.Trim());

            if (fondo.Estado == EstadoFondo.EnReposicion)
            {
                fondo.Estado = EstadoFondo.Activo;
            }

            if (!await _contexto.IntentarGuardarCambiosAsync(cancellationToken))
            {
                return ResultadoOperacion<Guid>.Fallo(
                    "Otro usuario modificó esta solicitud o el fondo mientras usted trabajaba. Recargue la pantalla e intente de nuevo.");
            }

            return ResultadoOperacion<Guid>.Ok(solicitud.Id);
        }

        private static List<string> Validar(SolicitudReposicion solicitud, FondoCajaChica fondo, string? motivo)
        {
            var errores = new List<string>();

            if (solicitud.Estado is not (EstadoReposicion.PendienteAprobacion or EstadoReposicion.DevueltaPorFinanzas))
            {
                errores.Add(
                    $"Solo se puede rechazar una solicitud pendiente de aprobación o devuelta por Finanzas. " +
                    $"Esta solicitud está en estado {solicitud.Estado}.");
            }

            ValidadorSolicitudReposicion.ValidarMotivo(errores, motivo, "del rechazo");
            ValidadorSolicitudReposicion.ValidarIntegridadSolicitud(errores, solicitud);
            ValidadorSolicitudReposicion.ValidarIntegridadFondo(errores, fondo);
            ValidadorSolicitudReposicion.ValidarGastosEnProceso(errores, solicitud);

            return errores;
        }
    }
}
