using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Domain.Constants;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;

namespace EDEEste.ControlCajaChica.Application.Features.Fondos
{
    /// <summary>
    /// Crea un fondo de caja chica. No inyecta ICurrentUserService: CreadoPorId lo
    /// pone AuditoriaInterceptor solo.
    /// </summary>
    public sealed class CrearFondoHandler
    {
        private readonly IFondoRepository _fondos;
        private readonly IIdentityService _identidad;
        private readonly IApplicationDbContext _contexto;

        public CrearFondoHandler(IFondoRepository fondos, IIdentityService identidad, IApplicationDbContext contexto)
        {
            _fondos = fondos;
            _identidad = identidad;
            _contexto = contexto;
        }

        public async Task<ResultadoOperacion<Guid>> EjecutarAsync(
            CrearFondoCommand comando,
            CancellationToken cancellationToken = default)
        {
            // La comprobacion de rol es asincrona y no puede vivir dentro de Validar
            // (estatico); se resuelve antes y entra como parametro, para no romper la
            // regla de acumular todos los errores en una sola pasada.
            var custodioId = comando.CustodioId?.Trim() ?? string.Empty;
            var custodioValido = custodioId.Length > 0 && await _identidad.EstaEnRolAsync(custodioId, RolesApp.Custodio);

            var errores = Validar(comando, custodioValido);
            if (errores.Count > 0)
            {
                return ResultadoOperacion<Guid>.Fallo(errores);
            }

            var fondo = new FondoCajaChica
            {
                MontoFijo = comando.MontoFijo,
                // Un fondo nace con el efectivo completo en caja.
                BalanceActual = comando.MontoFijo,
                LimitePorGasto = comando.LimitePorGasto,
                PorcentajeMaximoPorGasto = comando.PorcentajeMaximoPorGasto,
                PorcentajeAlertaReposicion = comando.PorcentajeAlertaReposicion,
                CustodioId = custodioId,
                Estado = EstadoFondo.Activo
            };

            await _fondos.AgregarAsync(fondo, cancellationToken);
            await _contexto.SaveChangesAsync(cancellationToken);

            return ResultadoOperacion<Guid>.Ok(fondo.Id);
        }

        private static List<string> Validar(CrearFondoCommand comando, bool custodioValido)
        {
            var errores = new List<string>();

            if (comando.MontoFijo <= 0)
            {
                errores.Add("El monto fijo debe ser mayor que cero.");
            }

            if (comando.PorcentajeMaximoPorGasto <= 0 || comando.PorcentajeMaximoPorGasto > 100)
            {
                errores.Add("El tope por gasto debe ser mayor que 0% y no puede superar el 100%.");
            }

            if (comando.LimitePorGasto < 0)
            {
                errores.Add("El limite por gasto no puede ser negativo.");
            }
            else if (comando.LimitePorGasto > 0)
            {
                var topeReglamentario = comando.MontoFijo * (comando.PorcentajeMaximoPorGasto / 100m);
                if (comando.LimitePorGasto > topeReglamentario)
                {
                    errores.Add(
                        $"El limite por gasto (RD$ {comando.LimitePorGasto:N2}) no puede superar el tope reglamentario " +
                        $"de RD$ {topeReglamentario:N2} ({comando.PorcentajeMaximoPorGasto:N1}% del monto fijo).");
                }
            }

            if (comando.PorcentajeAlertaReposicion < 10 || comando.PorcentajeAlertaReposicion > 50)
            {
                errores.Add("El porcentaje de alerta para reposicion debe estar entre 10% y 50%.");
            }

            if (string.IsNullOrWhiteSpace(comando.CustodioId))
            {
                errores.Add("Debe asignar un custodio.");
            }
            else if (!custodioValido)
            {
                errores.Add("El usuario indicado como custodio no tiene el rol de Custodio.");
            }

            return errores;
        }
    }
}
