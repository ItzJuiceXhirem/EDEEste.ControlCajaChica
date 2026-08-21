using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;

namespace EDEEste.ControlCajaChica.Application.Features.Reposiciones
{
    /// <summary>
    /// Registra el pago de una solicitud de reposicion aprobada. Es el unico punto de
    /// todo el sistema donde el efectivo vuelve al fondo.
    ///
    /// No inyecta IFondoRepository a proposito: ObtenerConDetalleAsync ya trae el
    /// fondo por Include, y comparte el mismo DbContext con scope, asi que pedirlo
    /// aparte devolveria la misma instancia por el mapa de identidad. Dos variables
    /// distintas apuntando al mismo objeto invitan a un "+=" duplicado si el codigo
    /// cambia mas adelante; con una sola referencia (solicitud.FondoCajaChica) ese
    /// error es estructuralmente imposible.
    /// </summary>
    public sealed class ProcesarPagoReposicionHandler
    {
        private const int LongitudMaximaReferencia = 100;

        private readonly IReposicionRepository _reposiciones;
        private readonly ICurrentUserService _usuarioActual;
        private readonly IApplicationDbContext _contexto;

        public ProcesarPagoReposicionHandler(
            IReposicionRepository reposiciones,
            ICurrentUserService usuarioActual,
            IApplicationDbContext contexto)
        {
            _reposiciones = reposiciones;
            _usuarioActual = usuarioActual;
            _contexto = contexto;
        }

        public async Task<ResultadoOperacion<Guid>> EjecutarAsync(
            ProcesarPagoReposicionCommand comando,
            CancellationToken cancellationToken = default)
        {
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

            // UNICO punto de todo el sistema donde el efectivo vuelve a la caja.
            fondo.BalanceActual += solicitud.MontoReclamado;

            foreach (var gasto in solicitud.Gastos)
            {
                gasto.Estado = EstadoGasto.Repuesto;
            }

            if (!await _contexto.IntentarGuardarCambiosAsync(cancellationToken))
            {
                return ResultadoOperacion<Guid>.Fallo(
                    "Otro usuario modifico esta solicitud o el fondo mientras usted trabajaba. Recargue la pantalla e intente de nuevo.");
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
                    $"Solo se puede pagar una solicitud aprobada. Esta solicitud esta en estado {solicitud.Estado}.");
            }

            var referencia = comando.ReferenciaPago?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(referencia))
            {
                errores.Add("Debe indicar la referencia del pago.");
            }
            else if (referencia.Length > LongitudMaximaReferencia)
            {
                errores.Add($"La referencia del pago no puede superar {LongitudMaximaReferencia} caracteres.");
            }

            if (solicitud.MontoReclamado <= 0)
            {
                errores.Add("El monto reclamado de la solicitud no es valido.");
            }

            // Guarda de techo: si esto se dispara, algo ya se conto dos veces (doble
            // pago, doble clic, dos usuarios de Finanzas a la vez).
            var balanceResultante = fondo.BalanceActual + solicitud.MontoReclamado;
            if (balanceResultante > fondo.MontoFijo)
            {
                errores.Add(
                    $"El pago dejaria el fondo en RD$ {balanceResultante:N2}, por encima del fondo fijo de " +
                    $"RD$ {fondo.MontoFijo:N2}. Verifique que la reposicion no se haya pagado ya.");
            }

            if (!solicitud.IntegridadVerificada)
            {
                errores.Add("La solicitud tiene la firma de integridad comprometida y no se puede procesar.");
            }

            if (!fondo.IntegridadVerificada)
            {
                errores.Add("El fondo tiene la firma de integridad comprometida.");
            }

            foreach (var gasto in solicitud.Gastos)
            {
                if (gasto.Estado != EstadoGasto.EnProcesoReposicion)
                {
                    errores.Add($"El gasto {gasto.NCF} no esta en proceso de reposicion; la solicitud esta inconsistente.");
                }

                if (!gasto.IntegridadVerificada)
                {
                    errores.Add($"El gasto {gasto.NCF} tiene la firma de integridad comprometida.");
                }
            }

            return errores;
        }
    }
}
