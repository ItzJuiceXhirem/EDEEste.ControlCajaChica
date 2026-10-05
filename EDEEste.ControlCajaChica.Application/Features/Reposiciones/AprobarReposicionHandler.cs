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
    /// Aprueba una solicitud de reposición: la pendiente de aprobación (primera
    /// aprobación) o la que Finanzas devolvió (aprobar de nuevo, con motivo).
    ///
    /// Al aprobar, el fondo pasa a EnReposicion si estaba Activo: desde ahí hasta que
    /// Finanzas pague o el Gerente rechace, el fondo tiene una reposición en camino.
    ///
    /// No inyecta IFondoRepository a propósito: aprobar no mueve dinero, y no tener el
    /// repositorio disponible lo hace evidente en la firma del constructor. El estado
    /// del fondo se cambia a través de solicitud.FondoCajaChica (mismo razonamiento que
    /// ProcesarPagoReposicionHandler). El efectivo solo vuelve al fondo cuando Finanzas
    /// paga.
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
                return ResultadoOperacion<Guid>.Fallo("No tiene permiso para aprobar una reposición.");
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

            var errores = Validar(comando, solicitud, fondo);
            if (errores.Count > 0)
            {
                return ResultadoOperacion<Guid>.Fallo(errores);
            }

            var usuario = await _usuarioActual.ObtenerAsync(cancellationToken);

            if (solicitud.Estado == EstadoReposicion.DevueltaPorFinanzas)
            {
                solicitud.MotivoReaprobacion = comando.Motivo!.Trim();
            }

            solicitud.GerenteUsuarioId = usuario.Id ?? string.Empty;
            solicitud.FechaAprobacion = DateTime.UtcNow;
            solicitud.Estado = EstadoReposicion.Aprobada;

            // Si venía devuelta, el fondo ya está EnReposicion y no hay nada que cambiar.
            if (fondo.Estado == EstadoFondo.Activo)
            {
                fondo.Estado = EstadoFondo.EnReposicion;
            }

            if (!await _contexto.IntentarGuardarCambiosAsync(cancellationToken))
            {
                return ResultadoOperacion<Guid>.Fallo(
                    "Otro usuario modificó esta solicitud o el fondo mientras usted trabajaba. Recargue la pantalla e intente de nuevo.");
            }

            return ResultadoOperacion<Guid>.Ok(solicitud.Id);
        }

        private static List<string> Validar(
            AprobarReposicionCommand comando,
            SolicitudReposicion solicitud,
            FondoCajaChica fondo)
        {
            var errores = new List<string>();

            if (solicitud.Estado is not (EstadoReposicion.PendienteAprobacion or EstadoReposicion.DevueltaPorFinanzas))
            {
                errores.Add(
                    $"Solo se puede aprobar una solicitud pendiente de aprobación o devuelta por Finanzas. " +
                    $"Esta solicitud está en estado {solicitud.Estado}.");
            }

            if (solicitud.Estado == EstadoReposicion.DevueltaPorFinanzas)
            {
                ValidadorSolicitudReposicion.ValidarMotivo(errores, comando.Motivo, "para aprobarla de nuevo");
            }

            ValidadorSolicitudReposicion.ValidarIntegridadSolicitud(errores, solicitud);
            ValidadorSolicitudReposicion.ValidarIntegridadFondo(errores, fondo);
            ValidadorSolicitudReposicion.ValidarGastosEnProceso(errores, solicitud);

            return errores;
        }
    }
}
