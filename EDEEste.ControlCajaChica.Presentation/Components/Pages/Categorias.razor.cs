using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Features.Categorias;
using EDEEste.ControlCajaChica.Domain.Entities;
using Microsoft.AspNetCore.Components;

namespace EDEEste.ControlCajaChica.Presentation.Components.Pages
{
    public partial class Categorias
    {
        [Inject]
        private ICategoriaGastoRepository RepositorioCategorias { get; set; } = default!;

        [Inject]
        private CrearCategoriaGastoHandler HandlerCrear { get; set; } = default!;

        [Inject]
        private ActualizarCategoriaGastoHandler HandlerActualizar { get; set; } = default!;

        private IReadOnlyList<CategoriaGasto>? categorias;
        private readonly EntradaCategoria entrada = new();
        private bool guardando;
        private Guid? cambiandoId;

        private Guid? categoriaEnEdicion;
        private EntradaEdicionCategoria? entradaEdicion;

        private readonly List<string> errores = new();
        private string? exito;

        protected override async Task OnInitializedAsync() => await RecargarAsync();

        private async Task RecargarAsync() => categorias = await RepositorioCategorias.ListarAsync();

        /// <summary>
        /// El check no es solo indicador: marcarlo activa o desactiva la categoría en
        /// el momento, a traves del mismo handler que usa la edicion completa (con el
        /// resto de los campos de la categoria sin cambiar).
        /// </summary>
        private async Task CambiarActivoAsync(CategoriaGasto categoria, bool activo)
        {
            errores.Clear();
            exito = null;
            cambiandoId = categoria.Id;

            try
            {
                var resultado = await HandlerActualizar.EjecutarAsync(new ActualizarCategoriaGastoCommand
                {
                    CategoriaGastoId = categoria.Id,
                    Nombre = categoria.Nombre,
                    CuentaContable = categoria.CuentaContable,
                    RequiereNCF = categoria.RequiereNCF,
                    Activo = activo
                });

                if (!resultado.Exitoso)
                {
                    errores.AddRange(resultado.Errores);
                    return;
                }

                exito = $"Categoría '{categoria.Nombre}' {(activo ? "activada" : "desactivada")}.";
                await RecargarAsync();
            }
            catch (Exception ex)
            {
                errores.Add(ex.Message);
            }
            finally
            {
                cambiandoId = null;
            }
        }

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

        private void IniciarEdicion(CategoriaGasto categoria)
        {
            errores.Clear();
            exito = null;
            categoriaEnEdicion = categoria.Id;
            entradaEdicion = new EntradaEdicionCategoria
            {
                Nombre = categoria.Nombre,
                CuentaContable = categoria.CuentaContable,
                RequiereNCF = categoria.RequiereNCF
            };
        }

        private void CancelarEdicion()
        {
            categoriaEnEdicion = null;
            entradaEdicion = null;
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
                    // El estado activo/inactivo se maneja con su propio check en la
                    // tabla; la edicion inline no lo toca.
                    Activo = categoria.Activo
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
        }
    }
}
