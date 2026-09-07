using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Features.Categorias;
using EDEEste.ControlCajaChica.Domain.Entities;
using Microsoft.AspNetCore.Components;

namespace EDEEste.ControlCajaChica.Presentation.Components.Pages
{
    public partial class Categorias
    {
        private enum Filtro { Todas, Activas, Inactivas }

        [Inject]
        private ICategoriaGastoRepository RepositorioCategorias { get; set; } = default!;

        [Inject]
        private IGastoRepository RepositorioGastos { get; set; } = default!;

        [Inject]
        private CrearCategoriaGastoHandler HandlerCrear { get; set; } = default!;

        [Inject]
        private ActualizarCategoriaGastoHandler HandlerActualizar { get; set; } = default!;

        private IReadOnlyList<CategoriaGasto>? categorias;
        private readonly EntradaCategoria entrada = new();
        private bool guardando;

        private Filtro filtro = Filtro.Todas;
        private string busqueda = string.Empty;

        private Guid? categoriaEnEdicion;
        private EntradaEdicionCategoria? entradaEdicion;

        // Se resuelve solo para la categoria que esta abierta en edicion -- no tiene
        // sentido contar gastos de las diez categorias en cada carga de pagina cuando
        // el dato solo se muestra de una a la vez.
        private int? usoDeLaCategoriaEnEdicion;

        private readonly List<string> errores = new();
        private string? exito;

        protected override async Task OnInitializedAsync() => await RecargarAsync();

        private async Task RecargarAsync() => categorias = await RepositorioCategorias.ListarAsync();

        private string Subtitulo
        {
            get
            {
                if (categorias is not { Count: > 0 })
                {
                    return "Todavía no hay ninguna categoría configurada.";
                }

                var activas = categorias.Count(c => c.Activo);
                return $"{categorias.Count} categorías · {activas} activas · {categorias.Count - activas} inactivas";
            }
        }

        /// <summary>
        /// Filtra en memoria: ListarAsync ya trae todas las categorías de una vez, y
        /// el catálogo es lo bastante pequeño para no justificar una consulta nueva
        /// por cada cambio de pestaña o cada tecla del buscador.
        /// </summary>
        private IEnumerable<CategoriaGasto> CategoriasFiltradas
        {
            get
            {
                if (categorias is null)
                {
                    return [];
                }

                var filtradas = filtro switch
                {
                    Filtro.Activas => categorias.Where(c => c.Activo),
                    Filtro.Inactivas => categorias.Where(c => !c.Activo),
                    _ => categorias.AsEnumerable()
                };

                return string.IsNullOrWhiteSpace(busqueda)
                    ? filtradas
                    : filtradas.Where(c => Coincide(c, busqueda.Trim()));
            }
        }

        private static bool Coincide(CategoriaGasto categoria, string busqueda) =>
            ContieneSinAcentos(categoria.Nombre, busqueda) || ContieneSinAcentos(categoria.CuentaContable, busqueda);

        private static bool ContieneSinAcentos(string texto, string busqueda) =>
            NormalizarParaBusqueda(texto).Contains(NormalizarParaBusqueda(busqueda), StringComparison.OrdinalIgnoreCase);

        private static string NormalizarParaBusqueda(string valor)
        {
            var normalizado = valor.Normalize(NormalizationForm.FormD);
            var sinDiacriticos = normalizado.Where(c =>
                CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);

            return new string(sinDiacriticos.ToArray()).Normalize(NormalizationForm.FormC);
        }

        private void CambiarFiltro(Filtro nuevo) => filtro = nuevo;

        private async Task CrearAsync()
        {
            errores.Clear();
            exito = null;
            guardando = true;

            try
            {
                var resultado = await HandlerCrear.EjecutarAsync(new CrearCategoriaGastoCommand
                {
                    Nombre = entrada.Nombre ?? string.Empty,
                    CuentaContable = entrada.CuentaContable ?? string.Empty,
                    RequiereNCF = entrada.RequiereNCF,
                    Activo = entrada.Activo
                });

                if (!resultado.Exitoso)
                {
                    errores.AddRange(resultado.Errores);
                    return;
                }

                exito = $"Categoría '{entrada.Nombre}' creada.";
                entrada.Limpiar();
                await RecargarAsync();
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

        private async Task IniciarEdicionAsync(CategoriaGasto categoria)
        {
            errores.Clear();
            exito = null;

            // La misma fila abre y cierra su edición (mismo espíritu que el botón
            // "Ver desglose ↔ Ocultar" de Arqueos): tocar "Cerrar" en la fila que ya
            // está abierta la pliega en vez de dejarla siempre desplegada.
            if (categoriaEnEdicion == categoria.Id)
            {
                CancelarEdicion();
                return;
            }

            categoriaEnEdicion = categoria.Id;
            entradaEdicion = new EntradaEdicionCategoria
            {
                Nombre = categoria.Nombre,
                CuentaContable = categoria.CuentaContable,
                RequiereNCF = categoria.RequiereNCF,
                Activo = categoria.Activo
            };

            usoDeLaCategoriaEnEdicion = await RepositorioGastos.ContarPorCategoriaYAnioAsync(categoria.Id, DateTime.Today.Year);
        }

        private void CancelarEdicion()
        {
            categoriaEnEdicion = null;
            entradaEdicion = null;
            usoDeLaCategoriaEnEdicion = null;
        }

        private async Task GuardarEdicionAsync(CategoriaGasto categoria)
        {
            if (categoriaEnEdicion is not { } id || entradaEdicion is null)
            {
                return;
            }

            errores.Clear();
            exito = null;
            guardando = true;

            try
            {
                var resultado = await HandlerActualizar.EjecutarAsync(new ActualizarCategoriaGastoCommand
                {
                    CategoriaGastoId = id,
                    Nombre = entradaEdicion.Nombre,
                    CuentaContable = entradaEdicion.CuentaContable,
                    RequiereNCF = entradaEdicion.RequiereNCF,
                    Activo = entradaEdicion.Activo
                });

                if (!resultado.Exitoso)
                {
                    errores.AddRange(resultado.Errores);
                    return;
                }

                exito = "Categoría actualizada.";
                CancelarEdicion();
                await RecargarAsync();
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

        private sealed class EntradaCategoria
        {
            public string? Nombre { get; set; }
            public string? CuentaContable { get; set; }
            public bool RequiereNCF { get; set; } = true;
            public bool Activo { get; set; } = true;

            public void Limpiar()
            {
                Nombre = null;
                CuentaContable = null;
                RequiereNCF = true;
                Activo = true;
            }
        }

        private sealed class EntradaEdicionCategoria
        {
            public string Nombre { get; set; } = string.Empty;
            public string CuentaContable { get; set; } = string.Empty;
            public bool RequiereNCF { get; set; }
            public bool Activo { get; set; }
        }
    }
}
