using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace EDEEste.ControlCajaChica.Presentation.Components.Pages
{
    public partial class Gastos
    {
        [Inject]
        private IFondoRepository Fondos { get; set; } = default!;

        [Inject]
        private IGastoRepository RepositorioGastos { get; set; } = default!;

        private IReadOnlyList<FondoCajaChica>? fondos;
        private IReadOnlyList<Gasto>? gastos;
        private Guid fondoSeleccionado;

        protected override async Task OnInitializedAsync()
        {
            fondos = await Fondos.ListarAsync();

            if (fondos.Count > 0)
            {
                fondoSeleccionado = fondos[0].Id;
                gastos = await RepositorioGastos.ListarPorFondoAsync(fondoSeleccionado);
            }
        }

        private async Task CambiarFondoAsync(ChangeEventArgs e)
        {
            if (Guid.TryParse(e.Value?.ToString(), out var id))
            {
                fondoSeleccionado = id;
                gastos = null;
                gastos = await RepositorioGastos.ListarPorFondoAsync(id);
            }
        }

        private static string ClaseEstado(EstadoGasto estado) => estado switch
        {
            EstadoGasto.PendienteReposicion => "text-bg-warning",
            EstadoGasto.EnProcesoReposicion => "text-bg-info",
            EstadoGasto.Repuesto => "text-bg-success",
            EstadoGasto.Rechazado or EstadoGasto.Anulado => "text-bg-danger",
            _ => "text-bg-secondary"
        };
    }
}
