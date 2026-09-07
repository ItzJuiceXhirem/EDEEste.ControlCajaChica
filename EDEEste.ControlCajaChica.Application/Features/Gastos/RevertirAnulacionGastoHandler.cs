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
    /// El Gerente decide que la anulacion que pidio el Custodio no procede: el gasto
    /// vuelve a PendienteReposicion tal como estaba. No inyecta IFondoRepository a
    /// proposito: como SolicitarAnulacionGastoHandler nunca abono el fondo, revertir
    /// tampoco tiene nada que devolver.
    /// </summary>
    public sealed class RevertirAnulacionGastoHandler
    {
        private readonly IGastoRepository _gastos;
        private readonly IAutorizacionService _autorizacion;
        private readonly IApplicationDbContext _contexto;

        public RevertirAnulacionGastoHandler(
            IGastoRepository gastos, IAutorizacionService autorizacion, IApplicationDbContext contexto)
        {
            _gastos = gastos;
            _autorizacion = autorizacion;
            _contexto = contexto;
        }

        public async Task<ResultadoOperacion<Guid>> EjecutarAsync(
            RevertirAnulacionGastoCommand comando,
            CancellationToken cancellationToken = default)
        {
            // Mismo permiso que anular directo: revertir es la misma autoridad
            // (Gerente) sobre el mismo expediente.
            if (!await _autorizacion.TienePermisoAsync(Permisos.AnularGasto, cancellationToken))
            {
                return ResultadoOperacion<Guid>.Fallo("No tiene permiso para revertir una anulación.");
            }

            var gasto = await _gastos.ObtenerPorIdAsync(comando.GastoId, cancellationToken);
            if (gasto is null)
            {
                return ResultadoOperacion<Guid>.Fallo("El gasto indicado no existe.");
            }

            var errores = Validar(gasto);
            if (errores.Count > 0)
            {
                return ResultadoOperacion<Guid>.Fallo(errores);
            }

            gasto.Estado = EstadoGasto.PendienteReposicion;

            // Se limpia el motivo: dejarlo puesto en un gasto que vuelve a estar vivo
            // haria creer que sigue anulado. El intento queda registrado en
            // LogAuditoria de todas formas.
            gasto.MotivoAnulacion = null;

            if (!await _contexto.IntentarGuardarCambiosAsync(cancellationToken))
            {
                return ResultadoOperacion<Guid>.Fallo(
                    "Otro usuario modifico este gasto mientras usted trabajaba. Recargue la pantalla e intente de nuevo.");
            }

            return ResultadoOperacion<Guid>.Ok(gasto.Id);
        }

        private static List<string> Validar(Gasto gasto)
        {
            var errores = new List<string>();

            if (gasto.Estado != EstadoGasto.AnulacionPendiente)
            {
                errores.Add(
                    $"Solo se puede revertir un gasto con anulacion pendiente. Este gasto esta en estado {gasto.Estado}.");
            }

            if (!gasto.IntegridadVerificada)
            {
                errores.Add("El gasto tiene la firma de integridad comprometida.");
            }

            return errores;
        }
    }
}
