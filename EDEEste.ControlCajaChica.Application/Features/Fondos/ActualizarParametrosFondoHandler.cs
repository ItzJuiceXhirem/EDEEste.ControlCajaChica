using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Domain.Constants;

namespace EDEEste.ControlCajaChica.Application.Features.Fondos
{
    public sealed class ActualizarParametrosFondoHandler
    {
        private readonly IFondoRepository _fondos;
        private readonly IIdentityService _identidad;
        private readonly IAutorizacionService _autorizacion;
        private readonly IApplicationDbContext _contexto;

        public ActualizarParametrosFondoHandler(
            IFondoRepository fondos, IIdentityService identidad, IAutorizacionService autorizacion, IApplicationDbContext contexto)
        {
            _fondos = fondos;
            _identidad = identidad;
            _autorizacion = autorizacion;
            _contexto = contexto;
        }

        public async Task<ResultadoOperacion<Guid>> EjecutarAsync(
            ActualizarParametrosFondoCommand comando,
            CancellationToken cancellationToken = default)
        {
            if (!await _autorizacion.TienePermisoAsync(Permisos.ConfigurarFondos, cancellationToken))
            {
                return ResultadoOperacion<Guid>.Fallo("No tiene permiso para configurar fondos.");
            }

            var fondo = await _fondos.ObtenerPorIdAsync(comando.FondoCajaChicaId, cancellationToken);
            if (fondo is null)
            {
                return ResultadoOperacion<Guid>.Fallo("El fondo indicado no existe.");
            }

            var custodioId = comando.CustodioId?.Trim() ?? string.Empty;
            var custodioValido = custodioId.Length > 0 && await _identidad.EstaEnRolAsync(custodioId, RolesApp.Custodio);

            // Se excluye este mismo fondo: no puede entrar en conflicto consigo mismo
            // cuando el custodio no cambia.
            var custodioYaTieneFondo = custodioId.Length > 0
                && await _fondos.ExisteFondoParaCustodioAsync(custodioId, fondo.Id, cancellationToken);

            var errores = Validar(comando, fondo.MontoFijo, custodioValido, custodioYaTieneFondo);
            if (errores.Count > 0)
            {
                return ResultadoOperacion<Guid>.Fallo(errores);
            }

            fondo.LimitePorGasto = comando.LimitePorGasto;
            fondo.PorcentajeMaximoPorGasto = comando.PorcentajeMaximoPorGasto;
            fondo.PorcentajeAlertaReposicion = comando.PorcentajeAlertaReposicion;
            fondo.CustodioId = custodioId;
            fondo.Estado = comando.Estado;

            // BalanceActual es token de concurrencia: este UPDATE lleva "AND
            // BalanceActual = @original" aunque esta operacion no lo cambie. Si un
            // gasto se registra a la vez, el Administrador recibe el error de
            // concurrencia y reintenta -- correcto y poco frecuente.
            if (!await _contexto.IntentarGuardarCambiosAsync(cancellationToken))
            {
                return ResultadoOperacion<Guid>.Fallo(
                    "Otro usuario modifico este fondo mientras usted trabajaba. Recargue la pantalla e intente de nuevo.");
            }

            return ResultadoOperacion<Guid>.Ok(fondo.Id);
        }

        private static List<string> Validar(
            ActualizarParametrosFondoCommand comando,
            decimal montoFijo,
            bool custodioValido,
            bool custodioYaTieneFondo)
        {
            var errores = new List<string>();

            if (comando.PorcentajeMaximoPorGasto < LimitesFondo.TopePorGastoMinimo
                || comando.PorcentajeMaximoPorGasto > LimitesFondo.TopePorGastoMaximo)
            {
                errores.Add(
                    $"El tope por gasto debe estar entre {LimitesFondo.TopePorGastoMinimo:N0}% y " +
                    $"{LimitesFondo.TopePorGastoMaximo:N0}%.");
            }

            if (comando.LimitePorGasto < 0)
            {
                errores.Add("El limite por gasto no puede ser negativo.");
            }
            else if (comando.LimitePorGasto > 0)
            {
                var topeReglamentario = montoFijo * (comando.PorcentajeMaximoPorGasto / 100m);
                if (comando.LimitePorGasto > topeReglamentario)
                {
                    errores.Add(
                        $"El limite por gasto (RD$ {comando.LimitePorGasto:N2}) no puede superar el tope reglamentario " +
                        $"de RD$ {topeReglamentario:N2} ({comando.PorcentajeMaximoPorGasto:N1}% del monto fijo).");
                }
            }

            if (comando.PorcentajeAlertaReposicion < LimitesFondo.AlertaReposicionMinima
                || comando.PorcentajeAlertaReposicion > LimitesFondo.AlertaReposicionMaxima)
            {
                errores.Add(
                    $"El porcentaje de alerta para reposicion debe estar entre " +
                    $"{LimitesFondo.AlertaReposicionMinima:N0}% y {LimitesFondo.AlertaReposicionMaxima:N0}%.");
            }

            if (string.IsNullOrWhiteSpace(comando.CustodioId))
            {
                errores.Add("Debe asignar un custodio.");
            }
            else if (!custodioValido)
            {
                errores.Add("El usuario indicado como custodio no tiene el rol de Custodio.");
            }
            else if (custodioYaTieneFondo)
            {
                errores.Add(
                    "Ese custodio ya tiene un fondo asignado: solo se permite un custodio por fondo " +
                    "y un fondo por custodio.");
            }

            if (!Enum.IsDefined(comando.Estado))
            {
                errores.Add("El estado del fondo no es valido.");
            }

            return errores;
        }
    }
}
