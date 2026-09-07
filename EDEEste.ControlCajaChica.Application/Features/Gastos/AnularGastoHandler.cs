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
    /// Anula un gasto y devuelve su monto al fondo. Es el unico handler del flujo de
    /// anulacion que inyecta IFondoRepository, precisamente porque es el unico que
    /// tiene autoridad para mover dinero (Gerente): tanto la anulacion directa como
    /// la confirmacion de una que pidio el Custodio pasan por aqui.
    /// </summary>
    public sealed class AnularGastoHandler
    {
        private const int LongitudMaximaMotivo = 500;

        private readonly IGastoRepository _gastos;
        private readonly IFondoRepository _fondos;
        private readonly IAutorizacionService _autorizacion;
        private readonly IApplicationDbContext _contexto;

        public AnularGastoHandler(
            IGastoRepository gastos, IFondoRepository fondos, IAutorizacionService autorizacion, IApplicationDbContext contexto)
        {
            _gastos = gastos;
            _fondos = fondos;
            _autorizacion = autorizacion;
            _contexto = contexto;
        }

        public async Task<ResultadoOperacion<Guid>> EjecutarAsync(
            AnularGastoCommand comando,
            CancellationToken cancellationToken = default)
        {
            if (!await _autorizacion.TienePermisoAsync(Permisos.AnularGasto, cancellationToken))
            {
                return ResultadoOperacion<Guid>.Fallo("No tiene permiso para anular un gasto.");
            }

            var gasto = await _gastos.ObtenerPorIdAsync(comando.GastoId, cancellationToken);
            if (gasto is null)
            {
                return ResultadoOperacion<Guid>.Fallo("El gasto indicado no existe.");
            }

            var fondo = await _fondos.ObtenerPorIdAsync(gasto.FondoCajaChicaId, cancellationToken);
            if (fondo is null)
            {
                return ResultadoOperacion<Guid>.Fallo("El fondo del gasto no existe.");
            }

            var esAnulacionDirecta = gasto.Estado == EstadoGasto.PendienteReposicion;

            var errores = Validar(comando, gasto, fondo, esAnulacionDirecta);
            if (errores.Count > 0)
            {
                return ResultadoOperacion<Guid>.Fallo(errores);
            }

            if (!string.IsNullOrWhiteSpace(comando.Motivo))
            {
                gasto.MotivoAnulacion = comando.Motivo.Trim();
            }

            gasto.Estado = EstadoGasto.Anulado;

            // Aqui vuelve el efectivo: es el unico momento del flujo de anulacion en
            // que alguien con autoridad lo aprueba. El paso AnulacionPendiente no
            // abona nada, asi que este += es el primero y el ultimo del flujo.
            fondo.BalanceActual += gasto.MontoTotal;

            if (!await _contexto.IntentarGuardarCambiosAsync(cancellationToken))
            {
                return ResultadoOperacion<Guid>.Fallo(
                    "Otro usuario modifico este gasto o el fondo mientras usted trabajaba. Recargue la pantalla e intente de nuevo.");
            }

            return ResultadoOperacion<Guid>.Ok(gasto.Id);
        }

        private static List<string> Validar(AnularGastoCommand comando, Gasto gasto, FondoCajaChica fondo, bool esAnulacionDirecta)
        {
            var errores = new List<string>();

            if (gasto.Estado is not (EstadoGasto.PendienteReposicion or EstadoGasto.AnulacionPendiente))
            {
                errores.Add(
                    $"Solo se puede anular un gasto pendiente de reposicion o con anulacion pendiente. " +
                    $"Este gasto esta en estado {gasto.Estado}.");
            }

            if (gasto.ReposicionId is not null)
            {
                errores.Add("El gasto ya forma parte de una solicitud de reposicion y no se puede anular.");
            }

            var motivo = comando.Motivo?.Trim() ?? string.Empty;
            if (esAnulacionDirecta && string.IsNullOrWhiteSpace(motivo))
            {
                errores.Add("Debe indicar el motivo de la anulacion.");
            }
            else if (motivo.Length > LongitudMaximaMotivo)
            {
                errores.Add($"El motivo de la anulacion no puede superar {LongitudMaximaMotivo} caracteres.");
            }

            // Guarda de techo: si esto se dispara, algo ya se conto dos veces.
            var balanceResultante = fondo.BalanceActual + gasto.MontoTotal;
            if (balanceResultante > fondo.MontoFijo)
            {
                errores.Add(
                    $"Devolver RD$ {gasto.MontoTotal:N2} dejaria el fondo en RD$ {balanceResultante:N2}, por encima " +
                    $"del fondo fijo de RD$ {fondo.MontoFijo:N2}.");
            }

            if (!gasto.IntegridadVerificada)
            {
                errores.Add("El gasto tiene la firma de integridad comprometida y no se puede anular.");
            }

            if (!fondo.IntegridadVerificada)
            {
                errores.Add("El fondo tiene la firma de integridad comprometida.");
            }

            return errores;
        }
    }
}
