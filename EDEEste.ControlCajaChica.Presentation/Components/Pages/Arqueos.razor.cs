using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Features.Arqueos;
using EDEEste.ControlCajaChica.Domain.Constants;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace EDEEste.ControlCajaChica.Presentation.Components.Pages
{
    public partial class Arqueos
    {
        [Inject]
        private IFondoRepository Fondos { get; set; } = default!;

        [Inject]
        private IGastoRepository RepositorioGastos { get; set; } = default!;

        // No puede llamarse "Arqueos": chocaria con el nombre de esta misma clase (CS0542).
        [Inject]
        private IArqueoRepository RepositorioArqueos { get; set; } = default!;

        [Inject]
        private RegistrarArqueoMensualHandler Handler { get; set; } = default!;

        [Inject]
        private IIdentityService Identidad { get; set; } = default!;

        private IReadOnlyList<FondoCajaChica>? fondos;
        private FondoCajaChica? fondoActual;
        private Guid fondoSeleccionado;

        // CustodioId guarda el Id de Identity (un GUID), no un nombre: sin este mapa,
        // el desplegable de fondos mostraria el GUID crudo en vez del nombre de usuario.
        private Dictionary<string, string> nombresDeCustodio = new();

        private IReadOnlyList<Gasto>? pendientes;
        private IReadOnlyList<ArqueoCaja>? historial;
        private ArqueoCaja? ultimoArqueo;
        private Guid? arqueoExpandido;

        // Solo guarda el Id: el texto completo se lee de "historial" al momento de
        // pintar el modal, para no duplicar el estado.
        private Guid? arqueoObservacionAbierta;
        private const int LongitudObservacionEnTabla = 60;

        private readonly List<FilaConteo> filas = CrearFilas();
        private DateTime fechaArqueo = DateTime.Today;
        private string observaciones = string.Empty;
        private bool guardando;

        private readonly List<string> errores = new();
        private string? exito;

        protected override async Task OnInitializedAsync()
        {
            fondos = await Fondos.ListarAsync();
            await ResolverNombresDeCustodioAsync();

            if (fondos.Count > 0)
            {
                fondoSeleccionado = fondos[0].Id;
                await CargarFondoAsync(fondoSeleccionado);
            }
        }

        private async Task ResolverNombresDeCustodioAsync()
        {
            var mapa = new Dictionary<string, string>();
            foreach (var id in fondos!.Select(f => f.CustodioId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct())
            {
                mapa[id] = await Identidad.ObtenerNombreUsuarioAsync(id) ?? id;
            }

            nombresDeCustodio = mapa;
        }

        private string NombreCustodio(string custodioId) =>
            string.IsNullOrWhiteSpace(custodioId) ? "(sin asignar)" : nombresDeCustodio.GetValueOrDefault(custodioId, custodioId);

        private async Task CambiarFondoAsync(ChangeEventArgs e)
        {
            if (Guid.TryParse(e.Value?.ToString(), out var id))
            {
                errores.Clear();
                exito = null;
                fondoSeleccionado = id;
                LimpiarFormulario();
                await CargarFondoAsync(id);
            }
        }

        private async Task CargarFondoAsync(Guid fondoId)
        {
            fondoActual = await Fondos.ObtenerPorIdAsync(fondoId);
            pendientes = await RepositorioGastos.ListarNoRepuestosAsync(fondoId);
            historial = await RepositorioArqueos.ListarPorFondoAsync(fondoId);
            ultimoArqueo = await RepositorioArqueos.ObtenerUltimoDelFondoAsync(fondoId);
        }

        private decimal MontoComprobantesPendientes => pendientes?.Sum(g => g.MontoTotal) ?? 0m;

        private decimal MontoContado => filas.Sum(f => f.Subtotal);

        private decimal SaldoTeorico => fondoActual?.BalanceActual ?? 0m;

        private decimal Diferencia => MontoContado - SaldoTeorico;

        /// <summary>
        /// La invariante del README (efectivo contado + comprobantes pendientes =
        /// fondo fijo), mostrada como una comprobacion en vivo mientras se cuenta.
        /// </summary>
        private bool InvarianteCuadra =>
            fondoActual is not null && MontoContado + MontoComprobantesPendientes == fondoActual.MontoFijo;

        private bool HayArqueoDelMesEnCurso =>
            ultimoArqueo is not null
            && ultimoArqueo.FechaArqueo.Year == DateTime.Today.Year
            && ultimoArqueo.FechaArqueo.Month == DateTime.Today.Month;

        private static List<FilaConteo> CrearFilas() =>
            DenominacionesRD.Todas.Select(valor => new FilaConteo { ValorDenominacion = valor }).ToList();

        private void LimpiarFormulario()
        {
            filas.Clear();
            filas.AddRange(CrearFilas());
            observaciones = string.Empty;
            fechaArqueo = DateTime.Today;
        }

        private void AlternarDetalle(Guid arqueoId) =>
            arqueoExpandido = arqueoExpandido == arqueoId ? null : arqueoId;

        private void VerObservacion(Guid arqueoId) => arqueoObservacionAbierta = arqueoId;

        private void CerrarObservacion() => arqueoObservacionAbierta = null;

        private ArqueoCaja? ArqueoConObservacionAbierta =>
            historial?.FirstOrDefault(a => a.Id == arqueoObservacionAbierta);

        /// <summary>
        /// Recorta el texto para que una observacion larga no deforme la tabla del
        /// historial. El texto completo queda a un clic de distancia en el modal.
        /// </summary>
        private static string TruncarObservacion(string? observaciones) =>
            string.IsNullOrEmpty(observaciones)
                ? "-"
                : observaciones.Length <= LongitudObservacionEnTabla
                    ? observaciones
                    : observaciones[..LongitudObservacionEnTabla] + "...";

        private static string ClaseResultado(ResultadoArqueo resultado) => resultado switch
        {
            ResultadoArqueo.Cuadrado => "text-bg-success",
            ResultadoArqueo.Sobrante => "text-bg-info",
            ResultadoArqueo.Faltante => "text-bg-danger",
            _ => "text-bg-secondary"
        };

        private static string EtiquetaResultado(ResultadoArqueo resultado) => resultado switch
        {
            ResultadoArqueo.Cuadrado => "Cuadrado",
            ResultadoArqueo.Sobrante => "Sobrante",
            ResultadoArqueo.Faltante => "Faltante",
            _ => resultado.ToString()
        };

        private async Task RegistrarAsync()
        {
            errores.Clear();
            exito = null;
            guardando = true;

            try
            {
                var resultado = await Handler.EjecutarAsync(new RegistrarArqueoMensualCommand
                {
                    FondoCajaChicaId = fondoSeleccionado,
                    FechaArqueo = fechaArqueo,
                    Observaciones = observaciones,
                    Denominaciones = filas.Select(f => new RegistrarArqueoMensualCommand.ConteoDenominacion
                    {
                        ValorDenominacion = f.ValorDenominacion,
                        Cantidad = f.Cantidad
                    }).ToList()
                });

                if (!resultado.Exitoso)
                {
                    errores.AddRange(resultado.Errores);
                    return;
                }

                exito = "Arqueo registrado.";
                LimpiarFormulario();
                await CargarFondoAsync(fondoSeleccionado);
            }
            catch (Exception ex)
            {
                errores.Add(ex.Message);
            }
            finally
            {
                guardando = false;
            }
        }

        private sealed class FilaConteo
        {
            public decimal ValorDenominacion { get; init; }
            public int Cantidad { get; set; }
            public decimal Subtotal => ValorDenominacion * Cantidad;
        }
    }
}
