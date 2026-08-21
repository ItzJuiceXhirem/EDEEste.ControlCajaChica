using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Entities;
using Microsoft.AspNetCore.Components;

namespace EDEEste.ControlCajaChica.Presentation.Components.Pages
{
    public partial class Categorias
    {
        [Inject]
        private ICategoriaGastoRepository RepositorioCategorias { get; set; } = default!;

        [Inject]
        private IApplicationDbContext Contexto { get; set; } = default!;

        private IReadOnlyList<CategoriaGasto>? categorias;
        private readonly EntradaCategoria entrada = new();
        private bool guardando;
        private Guid? cambiandoId;
        private string? mensaje;
        private string? error;

        protected override async Task OnInitializedAsync() => await RecargarAsync();

        /// <summary>
        /// La categoría viene rastreada por el mismo DbContext con scope que usa el
        /// repositorio, así que basta con cambiar la propiedad y guardar.
        /// </summary>
        private async Task CambiarActivoAsync(CategoriaGasto categoria, bool activo)
        {
            mensaje = null;
            error = null;
            cambiandoId = categoria.Id;

            try
            {
                categoria.Activo = activo;
                await Contexto.SaveChangesAsync();
                mensaje = $"Categoría '{categoria.Nombre}' {(activo ? "activada" : "desactivada")}.";
            }
            catch (Exception ex)
            {
                // Si no se pudo guardar, se devuelve el check a como estaba para que la
                // pantalla no muestre un estado que la BDD no tiene.
                categoria.Activo = !activo;
                error = ex.Message;
            }
            finally
            {
                cambiandoId = null;
            }
        }

        private async Task RecargarAsync() => categorias = await RepositorioCategorias.ListarAsync();

        private async Task CrearAsync()
        {
            mensaje = null;
            error = null;

            if (string.IsNullOrWhiteSpace(entrada.Nombre))
            {
                error = "El nombre es obligatorio.";
                return;
            }

            guardando = true;
            try
            {
                await RepositorioCategorias.AgregarAsync(new CategoriaGasto
                {
                    Nombre = entrada.Nombre!.Trim(),
                    CuentaContable = entrada.CuentaContable?.Trim() ?? string.Empty,
                    RequiereNCF = entrada.RequiereNCF,
                    Activo = entrada.Activo
                });
                await Contexto.SaveChangesAsync();

                mensaje = $"Categoría '{entrada.Nombre}' creada.";
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
    }
}
