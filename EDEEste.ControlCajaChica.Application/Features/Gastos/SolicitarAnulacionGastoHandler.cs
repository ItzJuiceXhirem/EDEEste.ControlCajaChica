using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;

namespace EDEEste.ControlCajaChica.Application.Features.Gastos
{
    /// <summary>
    /// El Custodio pide anular un gasto. Deja el gasto en AnulacionPendiente sin
    /// devolver el dinero: solo el Gerente puede confirmar la anulacion
    /// (AnularGastoHandler) y es ahi donde el efectivo vuelve al fondo.
    ///
    /// No inyecta IFondoRepository ni ICurrentUserService (mas alla de para
    /// AuditableEntity, que lo pone el interceptor) a proposito: este paso es
    /// estructuralmente incapaz de abonar el fondo, porque ni siquiera tiene el
    /// repositorio disponible para hacerlo.
    /// </summary>
    public sealed class SolicitarAnulacionGastoHandler
    {
        private const int LongitudMaximaMotivo = 500;

        private readonly IGastoRepository _gastos;
        private readonly IApplicationDbContext _contexto;

        public SolicitarAnulacionGastoHandler(IGastoRepository gastos, IApplicationDbContext contexto)
        {
            _gastos = gastos;
            _contexto = contexto;
        }

        public async Task<ResultadoOperacion<Guid>> EjecutarAsync(
            SolicitarAnulacionGastoCommand comando,
            CancellationToken cancellationToken = default)
        {
            var gasto = await _gastos.ObtenerPorIdAsync(comando.GastoId, cancellationToken);
            if (gasto is null)
            {
                return ResultadoOperacion<Guid>.Fallo("El gasto indicado no existe.");
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
