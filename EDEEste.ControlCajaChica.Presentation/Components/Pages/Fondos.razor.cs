using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace EDEEste.ControlCajaChica.Presentation.Components.Pages
{
    public partial class Fondos
    {
        [Inject]
        private IFondoRepository RepositorioFondos { get; set; } = default!;

        [Inject]
        private IApplicationDbContext Contexto { get; set; } = default!;

        private IReadOnlyList<FondoCajaChica>? fondos;
        private readonly EntradaFondo entrada = new();
        private bool guardando;
        private string? mensaje;
        private string? error;

        protected override async Task OnInitializedAsync() => await RecargarAsync();

        private async Task RecargarAsync() => fondos = await RepositorioFondos.ListarAsync();

        private static decimal LimiteEfectivo(FondoCajaChica fondo)
        {
            var tope = fondo.MontoFijo * 0.025m;
            return fondo.LimitePorGasto > 0 ? Math.Min(fondo.LimitePorGasto, tope) : tope;
        }

        private async Task CrearAsync()
        {
            mensaje = null;
            error = null;

            if (entrada.MontoFijo <= 0)
            {
                error = "El monto fijo debe ser mayor que cero.";
                return;
            }

            guardando = true;
            try
            {
                var fondo = new FondoCajaChica
                {
                    MontoFijo = entrada.MontoFijo,
                    // Un fondo nace con el efectivo completo en caja.
                    BalanceActual = entrada.MontoFijo,
                    LimitePorGasto = entrada.LimitePorGasto,
                    PorcentajeAlertaReposicion = entrada.PorcentajeAlertaReposicion,
                    CustodioId = entrada.CustodioId?.Trim() ?? string.Empty,
                    Estado = EstadoFondo.Activo
                };

                await RepositorioFondos.AgregarAsync(fondo);
                await Contexto.SaveChangesAsync();

                mensaje = $"Fondo creado con RD$ {fondo.MontoFijo:N2}.";
                entrada.Limpiar();
                await RecargarAsync();
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            finally
            {
                guardando = false;
            }
        }

        private sealed class EntradaFondo
        {
            public decimal MontoFijo { get; set; }
            public decimal LimitePorGasto { get; set; }
            public decimal PorcentajeAlertaReposicion { get; set; } = 30m;
            public string? CustodioId { get; set; }

            public void Limpiar()
            {
                MontoFijo = 0;
                LimitePorGasto = 0;
                PorcentajeAlertaReposicion = 30m;
                CustodioId = null;
            }
        }
    }
}
