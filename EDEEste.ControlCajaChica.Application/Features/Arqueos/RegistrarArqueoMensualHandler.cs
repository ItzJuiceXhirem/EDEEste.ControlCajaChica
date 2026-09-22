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

namespace EDEEste.ControlCajaChica.Application.Features.Arqueos
{
    /// <summary>
    /// Registra el conteo físico de un fondo. El arqueo MIDE, no corrige: si el
    /// conteo no cuadra con el saldo teórico, el resultado queda como Sobrante o
    /// Faltante y ninguna línea del fondo se toca -- corregir el balance aquí sería
    /// exactamente el agujero que el arqueo existe para detectar.
    /// </summary>
    public sealed class RegistrarArqueoMensualHandler
    {
        private const int LongitudMaximaObservaciones = 1000;

        private readonly IFondoRepository _fondos;
        private readonly IGastoRepository _gastos;
        private readonly IArqueoRepository _arqueos;
        private readonly ICurrentUserService _usuarioActual;
        private readonly IIdentityService _identidad;
        private readonly IAutorizacionService _autorizacion;
        private readonly IApplicationDbContext _contexto;

        public RegistrarArqueoMensualHandler(
            IFondoRepository fondos,
            IGastoRepository gastos,
            IArqueoRepository arqueos,
            ICurrentUserService usuarioActual,
            IIdentityService identidad,
            IAutorizacionService autorizacion,
            IApplicationDbContext contexto)
        {
            _fondos = fondos;
            _gastos = gastos;
            _arqueos = arqueos;
            _usuarioActual = usuarioActual;
            _identidad = identidad;
            _autorizacion = autorizacion;
            _contexto = contexto;
        }

        public async Task<ResultadoOperacion<Guid>> EjecutarAsync(
            RegistrarArqueoMensualCommand comando,
            CancellationToken cancellationToken = default)
        {
            if (!await _autorizacion.TienePermisoAsync(Permisos.EjecutarArqueo, cancellationToken))
            {
                return ResultadoOperacion<Guid>.Fallo("No tiene permiso para registrar un arqueo.");
            }

            var fondo = await _fondos.ObtenerPorIdAsync(comando.FondoCajaChicaId, cancellationToken);
            if (fondo is null)
            {
                return ResultadoOperacion<Guid>.Fallo("El fondo indicado no existe.");
            }

            var usuario = await _usuarioActual.ObtenerAsync(cancellationToken);

          /* El Id resuelto también queda como RealizadoPorUsuarioId más abajo -- sin
             poder identificarlo, esto falla cerrado en vez de dejar un registro con
             el campo vacío (y sin poder verificar tampoco que el fondo sea el suyo). */
            if (usuario.Id is not { } usuarioIdArquear)
            {
                return ResultadoOperacion<Guid>.Fallo("No se pudo identificar al usuario actual.");
            }

          /* Defensa en profundidad: sin esto, un Custodio podría arquear el fondo de
             otro custodio armando la petición contra el circuito de Blazor Server,
             aunque la pantalla ya solo le ofrezca el suyo en el desplegable. */
            if (await _identidad.EstaEnRolAsync(usuarioIdArquear, RolesApp.Custodio) && fondo.CustodioId != usuarioIdArquear)
            {
                return ResultadoOperacion<Guid>.Fallo("No tiene permiso para arquear el fondo de otro custodio.");
            }

            var errores = Validar(comando, fondo);
            if (errores.Count > 0)
            {
                return ResultadoOperacion<Guid>.Fallo(errores);
            }

          /* Se materializa la lista (y no un SumAsync) a propósito: al traer los
             gastos, IntegridadInterceptor valida la firma de cada uno. Un agregado
             calculado en la BDD sería mas rápido, pero un gasto manipulado entraría
             en el arqueo sin que nadie lo notara. */
            var noRepuestos = await _gastos.ListarNoRepuestosAsync(fondo.Id, cancellationToken);
            var montoComprobantesPendientes = noRepuestos.Sum(g => g.MontoTotal);

            var montoContado = comando.Denominaciones.Sum(d => d.ValorDenominacion * d.Cantidad);
            var saldoTeorico = fondo.BalanceActual;
            var diferencia = montoContado - saldoTeorico;

            var resultado = diferencia == 0m
                ? ResultadoArqueo.Cuadrado
                : diferencia > 0m
                    ? ResultadoArqueo.Sobrante
                    : ResultadoArqueo.Faltante;

            var arqueo = new ArqueoCaja
            {
                FondoCajaChicaId = fondo.Id,
                FechaArqueo = comando.FechaArqueo,
                MontoEfectivoContado = montoContado,
                MontoComprobantesPendientes = montoComprobantesPendientes,
                SaldoTeorico = saldoTeorico,
                Diferencia = diferencia,
                Resultado = resultado,
                Observaciones = string.IsNullOrWhiteSpace(comando.Observaciones) ? null : comando.Observaciones.Trim(),
                RealizadoPorUsuarioId = usuarioIdArquear
            };

          /* Solo se guardan las denominaciones que de verdad se contaron: una fila
             en cero no aporta nada al expediente y solo infla la tabla de detalle. */
            foreach (var denominacion in comando.Denominaciones.Where(d => d.Cantidad > 0))
            {
                arqueo.DetallesDenominacion.Add(new DetalleArqueoDenominacion
                {
                    ArqueoCajaId = arqueo.Id,
                    ValorDenominacion = denominacion.ValorDenominacion,
                    Cantidad = denominacion.Cantidad
                });
            }

            await _arqueos.AgregarAsync(arqueo, cancellationToken);
            await _contexto.SaveChangesAsync(cancellationToken);

            return ResultadoOperacion<Guid>.Ok(arqueo.Id);
        }

        private static List<string> Validar(RegistrarArqueoMensualCommand comando, FondoCajaChica fondo)
        {
            var errores = new List<string>();

            if (fondo.Estado != EstadoFondo.Activo)
            {
                errores.Add("El fondo no está activo, no admite arqueos.");
            }

            if (comando.FechaArqueo.Date > DateTime.Today)
            {
                errores.Add("La fecha del arqueo no puede ser futura.");
            }

            if (comando.Denominaciones.Count == 0)
            {
                errores.Add("Debe indicar el conteo por denominación.");
            }

          /* Se validan todas las filas, no solo las que tienen cantidad > 0: un
             total contado en cero es un resultado legítimo (una caja vacía), así
             que "sin denominaciones" solo significa que no se envió la lista. */
            var valoresVistos = new HashSet<decimal>();
            foreach (var denominacion in comando.Denominaciones)
            {
                if (denominacion.Cantidad < 0)
                {
                    errores.Add($"La cantidad de la denominación RD$ {denominacion.ValorDenominacion:N2} no puede ser negativa.");
                }

                if (!DenominacionesRD.Todas.Contains(denominacion.ValorDenominacion))
                {
                    errores.Add($"RD$ {denominacion.ValorDenominacion:N2} no es una denominación válida del peso dominicano.");
                }

                if (!valoresVistos.Add(denominacion.ValorDenominacion))
                {
                    errores.Add($"La denominación RD$ {denominacion.ValorDenominacion:N2} está repetida en el conteo.");
                }
            }

            var observaciones = comando.Observaciones?.Trim() ?? string.Empty;
            if (observaciones.Length > LongitudMaximaObservaciones)
            {
                errores.Add($"Las observaciones no pueden superar {LongitudMaximaObservaciones} caracteres.");
            }

            if (!fondo.IntegridadVerificada)
            {
                errores.Add("El fondo tiene la firma de integridad comprometida y no se puede arquear.");
            }

            return errores;
        }
    }
}
