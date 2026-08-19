using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Features.Reposiciones;
using EDEEste.ControlCajaChica.Domain.Entities;
using Microsoft.AspNetCore.Components;

namespace EDEEste.ControlCajaChica.Presentation.Components.Pages
{
    public partial class Reposiciones
    {
        private const decimal PorcentajeAlertaPorDefecto = 30m;

        [Inject]
        private IFondoRepository Fondos { get; set; } = default!;

        [Inject]
        private IGastoRepository Gastos { get; set; } = default!;

        [Inject]
        private IReposicionRepository RepositorioReposiciones { get; set; } = default!;

        [Inject]
        private CrearSolicitudReposicionHandler Handler { get; set; } = default!;

        private IReadOnlyList<FondoCajaChica>? fondos;
        private IReadOnlyList<Gasto>? pendientes;
        private IReadOnlyList<SolicitudReposicion>? solicitudes;
        private FondoCajaChica? fondoActual;
        private Guid fondoSeleccionado;

        private readonly List<string> errores = new();
        private string? exito;
        private bool generando;

        protected override async Task OnInitializedAsync()
        {
            fondos = await Fondos.ListarAsync();

            if (fondos.Count > 0)
            {
                fondoSeleccionado = fondos[0].Id;
                await CargarFondoAsync(fondoSeleccionado);
            }
        }

        private static decimal PorcentajeAlerta(FondoCajaChica fondo) =>
            fondo.PorcentajeAlertaReposicion > 0 ? fondo.PorcentajeAlertaReposicion : PorcentajeAlertaPorDefecto;

        private async Task CambiarFondoAsync(ChangeEventArgs e)
        {
            if (Guid.TryParse(e.Value?.ToString(), out var id))
            {
                errores.Clear();
                exito = null;
                fondoSeleccionado = id;
                await CargarFondoAsync(id);
            }
        }

        private async Task CargarFondoAsync(Guid fondoId)
        {
            pendientes = null;
            solicitudes = null;

            fondoActual = await Fondos.ObtenerPorIdAsync(fondoId);
            pendientes = await Gastos.ListarPendientesDeReposicionAsync(fondoId);
            solicitudes = await RepositorioReposiciones.ListarPorFondoAsync(fondoId);
        }

        private async Task GenerarAsync()
        {
            errores.Clear();
            exito = null;
            generando = true;

            try
            {
                var resultado = await Handler.EjecutarAsync(new CrearSolicitudReposicionCommand
                {
                    FondoCajaChicaId = fondoSeleccionado
                });

                if (!resultado.Exitoso)
                {
                    errores.AddRange(resultado.Errores);
                    return;
                }

                exito = "Solicitud creada. El expediente PDF ya está disponible para descargar.";
                await CargarFondoAsync(fondoSeleccionado);
            }
            catch (Exception ex)
            {
                errores.Add(ex.Message);
            }
            finally
            {
                generando = false;
            }
        }
    }
}
