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
    /// Registra el pago de una solicitud de reposición aprobada. Es el único punto de
    /// todo el sistema donde el efectivo vuelve al fondo.
    ///
    /// No inyecta IFondoRepository a propósito: ObtenerConDetalleAsync ya trae el
    /// fondo por Include, y comparte el mismo DbContext con scope, así que pedirlo
    /// aparte devolvería la misma instancia por el mapa de identidad. Dos variables
    /// distintas apuntando al mismo objeto invitan a un "+=" duplicado si el codigo
    /// cambia más adelante; con una sola referencia (solicitud.FondoCajaChica) ese
    /// error es estructuralmente imposible.
    /// </summary>
    public sealed class ProcesarPagoReposicionHandler
    {
        private readonly IReposicionRepository _reposiciones;
        private readonly ICurrentUserService _usuarioActual;
        private readonly IAutorizacionService _autorizacion;
        private readonly IApplicationDbContext _contexto;

        public ProcesarPagoReposicionHandler(
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
            ProcesarPagoReposicionCommand comando,
            CancellationToken cancellationToken = default)
        {
            if (!await _autorizacion.TienePermisoAsync(Permisos.PagarReposicion, cancellationToken))
            {
                return ResultadoOperacion<Guid>.Fallo("No tiene permiso para registrar el pago de una reposición.");
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

            solicitud.Estado = EstadoReposicion.Pagada;
            solicitud.FinanzasUsuarioId = usuario.Id ?? string.Empty;
            solicitud.FechaPago = DateTime.UtcNow;
            solicitud.ReferenciaPago = comando.ReferenciaPago.Trim();

            // ÚNICO punto de todo el sistema donde el efectivo vuelve a la caja.
            fondo.BalanceActual += solicitud.MontoReclamado;

            // Con el pago termina la reposición en camino: el fondo vuelve a Activo.
            if (fondo.Estado == EstadoFondo.EnReposicion)
            {
                fondo.Estado = EstadoFondo.Activo;
            }

            foreach (var gasto in solicitud.Gastos)
            {
                gasto.Estado = EstadoGasto.Repuesto;
            }

            if (!await _contexto.IntentarGuardarCambiosAsync(cancellationToken))
            {
                return ResultadoOperacion<Guid>.Fallo(
                    "Otro usuario modificó esta solicitud o el fondo mientras usted trabajaba. Recargue la pantalla e intente de nuevo.");
            }

            return ResultadoOperacion<Guid>.Ok(solicitud.Id);
        }

        private static List<string> Validar(
            ProcesarPagoReposicionCommand comando,
            SolicitudReposicion solicitud,
            FondoCajaChica fondo)
        {
            var errores = new List<string>();

            if (solicitud.Estado != EstadoReposicion.Aprobada)
            {
                errores.Add(
                    $"Solo se puede pagar una solicitud aprobada. Esta solicitud está en estado {solicitud.Estado}.");
            }

            var referencia = comando.ReferenciaPago?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(referencia))
            {
                errores.Add("Debe indicar la referencia del pago.");
            }
            else if (referencia.Length > LimitesReposicion.LongitudMaximaReferenciaPago)
            {
                errores.Add(
                    $"La referencia del pago no puede superar {LimitesReposicion.LongitudMaximaReferenciaPago} caracteres.");
            }

            if (solicitud.MontoReclamado <= 0)
            {
                errores.Add("El monto reclamado de la solicitud no es válido.");
            }

          /* Guarda de techo: si esto se dispara, algo ya se contó dos veces (doble
             pago, doble clic, dos usuarios de Finanzas a la vez). */
            var balanceResultante = fondo.BalanceActual + solicitud.MontoReclamado;
            if (balanceResultante > fondo.MontoFijo)
            {
                errores.Add(
                    $"El pago dejaría el fondo en RD$ {balanceResultante:N2}, por encima del fondo fijo de " +
                    $"RD$ {fondo.MontoFijo:N2}. Verifique que la reposición no se haya pagado ya.");
            }

            ValidadorSolicitudReposicion.ValidarIntegridadSolicitud(errores, solicitud);
            ValidadorSolicitudReposicion.ValidarIntegridadFondo(errores, fondo);
            ValidadorSolicitudReposicion.ValidarGastosEnProceso(errores, solicitud);

            return errores;
        }
    }
}
