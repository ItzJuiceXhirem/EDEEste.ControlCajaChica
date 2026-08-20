using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Features.Gastos;
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

        [Inject]
        private SolicitarAnulacionGastoHandler HandlerSolicitarAnulacion { get; set; } = default!;

        [Inject]
        private AnularGastoHandler HandlerAnular { get; set; } = default!;

        [Inject]
        private RevertirAnulacionGastoHandler HandlerRevertirAnulacion { get; set; } = default!;

        private IReadOnlyList<FondoCajaChica>? fondos;
        private IReadOnlyList<Gasto>? gastos;
        private IReadOnlyList<Gasto>? anulacionesPendientes;
        private IReadOnlyList<Gasto>? anulados;
        private Guid fondoSeleccionado;

        // null = todo el historial.
        private int? diasAnulados = 90;
        private string busquedaAnulados = string.Empty;

        // Fila expandida con el textarea de motivo, y el motivo que se esta escribiendo.
        // anulacionDirectaEnCurso distingue si el formulario abierto es "Solicitar
        // anulacion" (Custodio, no toca el balance) o "Anular" directo (Gerente, si
        // lo toca) -- misma fila de UI, pero el boton "Confirmar" llama a un handler
        // distinto segun cual se haya pulsado.
        private Guid? gastoEnAnulacion;
        private bool anulacionDirectaEnCurso;
        private string motivoAnulacion = string.Empty;

        // Bloquea todos los botones de accion mientras se procesa un gasto.
        private Guid? procesando;

        private readonly List<string> errores = new();
        private string? exito;

        protected override async Task OnInitializedAsync()
        {
            fondos = await Fondos.ListarAsync();

            if (fondos.Count > 0)
            {
                fondoSeleccionado = fondos[0].Id;
                await CargarTodoAsync(fondoSeleccionado);
            }
        }

        private async Task CambiarFondoAsync(ChangeEventArgs e)
        {
            if (Guid.TryParse(e.Value?.ToString(), out var id))
            {
                errores.Clear();
                exito = null;
                fondoSeleccionado = id;
                await CargarTodoAsync(id);
            }
        }

        private async Task CargarTodoAsync(Guid fondoId)
        {
            gastos = null;
            anulacionesPendientes = null;
            anulados = null;

            gastos = await RepositorioGastos.ListarPorFondoAsync(fondoId);
            anulacionesPendientes = await RepositorioGastos.ListarPorEstadoAsync(fondoId, EstadoGasto.AnulacionPendiente);
            await CargarAnuladosAsync(fondoId);
        }

        private async Task CargarAnuladosAsync(Guid fondoId)
        {
            var desde = diasAnulados is { } dias ? DateTime.UtcNow.AddDays(-dias) : (DateTime?)null;
            anulados = await RepositorioGastos.ListarAnuladosAsync(fondoId, desde, default);
        }

        private async Task CambiarVentanaAnuladosAsync(ChangeEventArgs e)
        {
            var valor = e.Value?.ToString();
            diasAnulados = string.IsNullOrEmpty(valor) ? null : int.Parse(valor, CultureInfo.InvariantCulture);
            await CargarAnuladosAsync(fondoSeleccionado);
        }

        /// <summary>
        /// Filtra en memoria sobre lo ya cargado, sin volver a la BDD: la lista ya
        /// esta acotada por la ventana de fechas, asi que el usuario ve resultados
        /// mientras teclea sin disparar una consulta por pulsacion.
        /// </summary>
        private IEnumerable<Gasto> AnuladosFiltrados =>
            anulados is null
                ? []
                : string.IsNullOrWhiteSpace(busquedaAnulados)
                    ? anulados
                    : anulados.Where(g => Coincide(g, busquedaAnulados.Trim()));

        private static bool Coincide(Gasto gasto, string busqueda)
        {
            var campos = new[]
            {
                gasto.Proveedor,
                gasto.NCF,
                gasto.RNCProveedor,
                gasto.Concepto,
                gasto.MotivoAnulacion,
                gasto.MontoTotal.ToString("N2", CultureInfo.InvariantCulture)
            };

            return campos.Any(campo => campo is not null && ContieneSinAcentos(campo, busqueda));
        }

        private static bool ContieneSinAcentos(string texto, string busqueda) =>
            NormalizarParaBusqueda(texto).Contains(NormalizarParaBusqueda(busqueda), StringComparison.OrdinalIgnoreCase);

        private static string NormalizarParaBusqueda(string valor)
        {
            var normalizado = valor.Normalize(NormalizationForm.FormD);
            var sinDiacriticos = normalizado.Where(c =>
                CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);

            return new string(sinDiacriticos.ToArray()).Normalize(NormalizationForm.FormC);
        }

        private static string ClaseEstado(EstadoGasto estado) => estado switch
        {
            EstadoGasto.PendienteReposicion => "text-bg-warning",
            EstadoGasto.EnProcesoReposicion => "text-bg-info",
            EstadoGasto.Repuesto => "text-bg-success",
            EstadoGasto.Rechazado or EstadoGasto.Anulado => "text-bg-danger",
            EstadoGasto.AnulacionPendiente => "text-bg-dark",
            _ => "text-bg-secondary"
        };

        private static string EtiquetaEstado(EstadoGasto estado) => estado switch
        {
            EstadoGasto.PendienteReposicion => "Pendiente de reposición",
            EstadoGasto.EnProcesoReposicion => "En proceso de reposición",
            EstadoGasto.Repuesto => "Repuesto",
            EstadoGasto.Rechazado => "Rechazado",
            EstadoGasto.Anulado => "Anulado",
            EstadoGasto.AnulacionPendiente => "Anulación pendiente",
            _ => estado.ToString()
        };

        private void AbrirFormularioAnulacion(Guid gastoId, bool directa)
        {
            gastoEnAnulacion = gastoId;
            anulacionDirectaEnCurso = directa;
            motivoAnulacion = string.Empty;
        }

        private void CancelarFormularioAnulacion()
        {
            gastoEnAnulacion = null;
            motivoAnulacion = string.Empty;
        }

        private Task ConfirmarFormularioAnulacionAsync()
        {
            if (gastoEnAnulacion is not { } gastoId)
            {
                return Task.CompletedTask;
            }

            return anulacionDirectaEnCurso
                ? AnularDirectoAsync(gastoId)
                : SolicitarAnulacionAsync(gastoId);
        }

        private async Task SolicitarAnulacionAsync(Guid gastoId)
        {
            errores.Clear();
            exito = null;
            procesando = gastoId;

            try
            {
                var resultado = await HandlerSolicitarAnulacion.EjecutarAsync(new SolicitarAnulacionGastoCommand
                {
                    GastoId = gastoId,
                    Motivo = motivoAnulacion
                });

                if (!resultado.Exitoso)
                {
                    errores.AddRange(resultado.Errores);
                    return;
                }

                exito = "Se pidió la anulación del gasto. Queda pendiente de que el Gerente la confirme.";
                gastoEnAnulacion = null;
                motivoAnulacion = string.Empty;
                await CargarTodoAsync(fondoSeleccionado);
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

        private async Task AnularDirectoAsync(Guid gastoId)
        {
            errores.Clear();
            exito = null;
            procesando = gastoId;

            try
            {
                var resultado = await HandlerAnular.EjecutarAsync(new AnularGastoCommand
                {
                    GastoId = gastoId,
                    Motivo = motivoAnulacion
                });

                if (!resultado.Exitoso)
                {
                    errores.AddRange(resultado.Errores);
                    return;
                }

                exito = "Gasto anulado. El monto volvió al fondo.";
                gastoEnAnulacion = null;
                motivoAnulacion = string.Empty;
                await CargarTodoAsync(fondoSeleccionado);
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

        private async Task ConfirmarAnulacionAsync(Guid gastoId)
        {
            errores.Clear();
            exito = null;
            procesando = gastoId;

            try
            {
                // Sin motivo: se conserva el que escribio el Custodio al pedirla.
                var resultado = await HandlerAnular.EjecutarAsync(new AnularGastoCommand { GastoId = gastoId });

                if (!resultado.Exitoso)
                {
                    errores.AddRange(resultado.Errores);
                    return;
                }

                exito = "Anulación confirmada. El monto volvió al fondo.";
                await CargarTodoAsync(fondoSeleccionado);
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

        private async Task RevertirAnulacionAsync(Guid gastoId)
        {
            errores.Clear();
            exito = null;
            procesando = gastoId;

            try
            {
                var resultado = await HandlerRevertirAnulacion.EjecutarAsync(new RevertirAnulacionGastoCommand { GastoId = gastoId });

                if (!resultado.Exitoso)
                {
                    errores.AddRange(resultado.Errores);
                    return;
                }

                exito = "Se devolvió el gasto a pendiente de reposición.";
                await CargarTodoAsync(fondoSeleccionado);
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
