using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Domain.Constants;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;

namespace EDEEste.ControlCajaChica.Application.Features.Gastos
{
    /// <summary>
    /// El Custodio pide anular un gasto. Deja el gasto en AnulacionPendiente sin
    /// devolver el dinero: solo el Gerente puede confirmar la anulacion
    /// (AnularGastoHandler) y es ahi donde el efectivo vuelve al fondo.
    ///
    /// IFondoRepository entra solo de lectura, para la comprobacion de pertenencia de
    /// fondo (defensa en profundidad): en ninguna parte de este handler se escribe
    /// FondoCajaChica.BalanceActual, asi que sigue siendo estructuralmente incapaz de
    /// abonar el fondo.
    /// </summary>
    public sealed class SolicitarAnulacionGastoHandler
    {
        private const int LongitudMaximaMotivo = 500;

        private readonly IGastoRepository _gastos;
        private readonly IFondoRepository _fondos;
        private readonly ICurrentUserService _usuarioActual;
        private readonly IIdentityService _identidad;
        private readonly IAutorizacionService _autorizacion;
        private readonly IApplicationDbContext _contexto;

        public SolicitarAnulacionGastoHandler(
            IGastoRepository gastos,
            IFondoRepository fondos,
            ICurrentUserService usuarioActual,
            IIdentityService identidad,
            IAutorizacionService autorizacion,
            IApplicationDbContext contexto)
        {
            _gastos = gastos;
            _fondos = fondos;
            _usuarioActual = usuarioActual;
            _identidad = identidad;
            _autorizacion = autorizacion;
            _contexto = contexto;
        }

        public async Task<ResultadoOperacion<Guid>> EjecutarAsync(
            SolicitarAnulacionGastoCommand comando,
            CancellationToken cancellationToken = default)
        {
            if (!await _autorizacion.TienePermisoAsync(Permisos.SolicitarAnulacionGasto, cancellationToken))
            {
                return ResultadoOperacion<Guid>.Fallo("No tiene permiso para solicitar la anulación de un gasto.");
            }

            var gasto = await _gastos.ObtenerPorIdAsync(comando.GastoId, cancellationToken);
            if (gasto is null)
            {
                return ResultadoOperacion<Guid>.Fallo("El gasto indicado no existe.");
            }

            var usuario = await _usuarioActual.ObtenerAsync(cancellationToken);

            // Defensa en profundidad: sin esto, un Custodio podria pedir la anulacion
            // de un gasto de otro fondo con solo conocer (o adivinar) su GastoId.
            if (usuario.Id is { } usuarioIdSolicitar
                && await _identidad.EstaEnRolAsync(usuarioIdSolicitar, RolesApp.Custodio))
            {
                var fondo = await _fondos.ObtenerPorIdAsync(gasto.FondoCajaChicaId, cancellationToken);
                if (fondo is null || fondo.CustodioId != usuarioIdSolicitar)
                {
                    return ResultadoOperacion<Guid>.Fallo("No tiene permiso para anular gastos del fondo de otro custodio.");
                }
            }

            var errores = Validar(comando, gasto);
            if (errores.Count > 0)
            {
                return ResultadoOperacion<Guid>.Fallo(errores);
            }

            gasto.Estado = EstadoGasto.AnulacionPendiente;
            gasto.MotivoAnulacion = comando.Motivo.Trim();

            if (!await _contexto.IntentarGuardarCambiosAsync(cancellationToken))
            {
                return ResultadoOperacion<Guid>.Fallo(
                    "Otro usuario modifico este gasto mientras usted trabajaba. Recargue la pantalla e intente de nuevo.");
            }

            return ResultadoOperacion<Guid>.Ok(gasto.Id);
        }

        private static List<string> Validar(SolicitarAnulacionGastoCommand comando, Gasto gasto)
        {
            var errores = new List<string>();

            if (gasto.Estado != EstadoGasto.PendienteReposicion)
            {
                errores.Add(
                    $"Solo se puede pedir la anulacion de un gasto pendiente de reposicion. " +
                    $"Este gasto esta en estado {gasto.Estado}.");
            }

            // Doble condicion a proposito, misma filosofia que
            // ListarPendientesDeReposicionAsync: si alguna vez se desincronizan, se
            // prefiere bloquear la anulacion antes que dejar anular un gasto que ya
            // esta en un expediente.
            if (gasto.ReposicionId is not null)
            {
                errores.Add("El gasto ya forma parte de una solicitud de reposicion y no se puede anular.");
            }

            var motivo = comando.Motivo?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(motivo))
            {
                errores.Add("Debe indicar el motivo de la anulacion.");
            }
            else if (motivo.Length > LongitudMaximaMotivo)
            {
                errores.Add($"El motivo de la anulacion no puede superar {LongitudMaximaMotivo} caracteres.");
            }

            if (!gasto.IntegridadVerificada)
            {
                errores.Add("El gasto tiene la firma de integridad comprometida y no se puede anular.");
            }

            return errores;
        }
    }
}
