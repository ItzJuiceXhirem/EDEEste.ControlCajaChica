using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Features.Reposiciones;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;
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

        [Inject]
        private AprobarReposicionHandler HandlerAprobar { get; set; } = default!;

        [Inject]
        private ProcesarPagoReposicionHandler HandlerPagar { get; set; } = default!;

        private IReadOnlyList<FondoCajaChica>? fondos;
        private IReadOnlyList<Gasto>? pendientes;
        private IReadOnlyList<SolicitudReposicion>? solicitudes;
        private IReadOnlyList<SolicitudReposicion>? porAprobar;
        private IReadOnlyList<SolicitudReposicion>? porPagar;
        private FondoCajaChica? fondoActual;
        private Guid fondoSeleccionado;

        // Bloquea todos los botones de accion mientras se procesa una solicitud, no
        // solo el de la fila que se pulso: dos clics sobre solicitudes distintas
        // podrian competer por el mismo fondo.
        private Guid? procesando;
        private readonly Dictionary<Guid, string> referencias = new();

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

            await CargarColasAsync();
        }

        /// <summary>
        /// Las colas de aprobacion y pago no dependen del fondo seleccionado: el
        /// Gerente y Finanzas trabajan por cola, no por fondo, y ni siquiera todos
        /// tienen el permiso para elegir un fondo.
        /// </summary>
        private async Task CargarColasAsync()
        {
            porAprobar = await RepositorioReposiciones.ListarPorEstadoAsync(EstadoReposicion.PendienteAprobacion);
            porPagar = await RepositorioReposiciones.ListarPorEstadoAsync(EstadoReposicion.Aprobada);
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

        private async Task AprobarAsync(Guid reposicionId, bool aprobar)
        {
            errores.Clear();
            exito = null;
            procesando = reposicionId;

            try
            {
                var resultado = await HandlerAprobar.EjecutarAsync(new AprobarReposicionCommand
                {
                    ReposicionId = reposicionId,
                    Aprobar = aprobar
                });

                if (!resultado.Exitoso)
                {
                    errores.AddRange(resultado.Errores);
                    return;
                }

                exito = aprobar
                    ? "Solicitud aprobada."
                    : "Solicitud rechazada. Los gastos volvieron a quedar pendientes de reposición.";

                // Recargar es obligatorio, no cosmetico: el DbContext vive todo el
                // circuito y las listas apuntan a entidades ya rastreadas.
                await CargarColasAsync();

                if (fondos is { Count: > 0 })
                {
                    await CargarFondoAsync(fondoSeleccionado);
                }
            }
            catch (Exception ex)
            {
                errores.Add(ex.Message);
            }
            finally
            {
                procesando = null;
            }
        }

        private async Task PagarAsync(Guid reposicionId)
        {
            errores.Clear();
            exito = null;
            procesando = reposicionId;

            try
            {
                referencias.TryGetValue(reposicionId, out var referencia);

                var resultado = await HandlerPagar.EjecutarAsync(new ProcesarPagoReposicionCommand
                {
                    ReposicionId = reposicionId,
                    ReferenciaPago = referencia ?? string.Empty
                });

                if (!resultado.Exitoso)
                {
                    errores.AddRange(resultado.Errores);
                    return;
                }

                exito = "Pago registrado. El efectivo volvió al fondo.";
                referencias.Remove(reposicionId);

                await CargarColasAsync();

                if (fondos is { Count: > 0 })
                {
                    await CargarFondoAsync(fondoSeleccionado);
                }
            }
            catch (Exception ex)
            {
                errores.Add(ex.Message);
            }
            finally
            {
                procesando = null;
            }
        }
    }
}
