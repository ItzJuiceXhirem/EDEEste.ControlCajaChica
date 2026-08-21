using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.DTOs;
using EDEEste.ControlCajaChica.Application.Features.Fondos;
using EDEEste.ControlCajaChica.Domain.Constants;
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
        private IIdentityService Identidad { get; set; } = default!;

        [Inject]
        private CrearFondoHandler HandlerCrear { get; set; } = default!;

        [Inject]
        private ActualizarParametrosFondoHandler HandlerActualizar { get; set; } = default!;

        private IReadOnlyList<FondoCajaChica>? fondos;
        private IReadOnlyList<UsuarioResumenDto>? custodios;

        // CustodioId guarda el Id de Identity (un GUID), no un nombre: sin este mapa,
        // la columna "Custodio" de la tabla mostraria el GUID crudo en vez del nombre
        // de usuario.
        private Dictionary<string, string> nombresDeUsuario = new();

        private readonly EntradaFondo entrada = new();
        private bool guardando;

        private Guid? fondoEnEdicion;
        private EntradaEdicionFondo? entradaEdicion;

        private readonly List<string> errores = new();
        private string? exito;

        protected override async Task OnInitializedAsync()
        {
            await RecargarAsync();

            var usuarios = await Identidad.ListarUsuariosAsync();
            nombresDeUsuario = usuarios.ToDictionary(u => u.Id, u => u.Usuario);

            // Solo cuentas ya aprobadas y con el rol Custodio: es la lista de la que
            // el Administrador puede elegir para asignar un fondo.
            custodios = usuarios
                .Where(u => u.EstadoAcceso == EstadoAccesoUsuario.Aprobado
                            && string.Equals(u.Rol, RolesApp.Custodio, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private async Task RecargarAsync() => fondos = await RepositorioFondos.ListarAsync();

        private string NombreCustodio(string custodioId) =>
            string.IsNullOrWhiteSpace(custodioId)
                ? "(sin asignar)"
                : nombresDeUsuario.GetValueOrDefault(custodioId, custodioId);

        private static decimal LimiteEfectivo(FondoCajaChica fondo)
        {
            var tope = fondo.MontoFijo * (fondo.PorcentajeMaximoPorGasto / 100m);
            return fondo.LimitePorGasto > 0 ? Math.Min(fondo.LimitePorGasto, tope) : tope;
        }

        private async Task CrearAsync()
        {
            errores.Clear();
            exito = null;
            guardando = true;

            try
            {
                var resultado = await HandlerCrear.EjecutarAsync(new CrearFondoCommand
                {
                    MontoFijo = entrada.MontoFijo,
                    LimitePorGasto = entrada.LimitePorGasto,
                    PorcentajeMaximoPorGasto = entrada.PorcentajeMaximoPorGasto,
                    PorcentajeAlertaReposicion = entrada.PorcentajeAlertaReposicion,
                    CustodioId = entrada.CustodioId ?? string.Empty
                });

                if (!resultado.Exitoso)
                {
                    errores.AddRange(resultado.Errores);
                    return;
                }

                exito = $"Fondo creado con RD$ {entrada.MontoFijo:N2}.";
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

        private void IniciarEdicion(FondoCajaChica fondo)
        {
            errores.Clear();
            exito = null;
            fondoEnEdicion = fondo.Id;
            entradaEdicion = new EntradaEdicionFondo
            {
                LimitePorGasto = fondo.LimitePorGasto,
                PorcentajeMaximoPorGasto = fondo.PorcentajeMaximoPorGasto,
                PorcentajeAlertaReposicion = fondo.PorcentajeAlertaReposicion,
                CustodioId = fondo.CustodioId,
                Estado = fondo.Estado
            };
        }

        private void CancelarEdicion()
        {
            fondoEnEdicion = null;
            entradaEdicion = null;
        }

        private async Task GuardarEdicionAsync()
        {
            if (fondoEnEdicion is not { } id || entradaEdicion is null)
            {
                return;
            }

            errores.Clear();
            exito = null;
            guardando = true;

            try
            {
                var resultado = await HandlerActualizar.EjecutarAsync(new ActualizarParametrosFondoCommand
                {
                    FondoCajaChicaId = id,
                    LimitePorGasto = entradaEdicion.LimitePorGasto,
                    PorcentajeMaximoPorGasto = entradaEdicion.PorcentajeMaximoPorGasto,
                    PorcentajeAlertaReposicion = entradaEdicion.PorcentajeAlertaReposicion,
                    CustodioId = entradaEdicion.CustodioId,
                    Estado = entradaEdicion.Estado
                });

                if (!resultado.Exitoso)
                {
                    errores.AddRange(resultado.Errores);
                    return;
                }

                exito = "Fondo actualizado.";
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

        private sealed class EntradaFondo
        {
            public decimal MontoFijo { get; set; }
            public decimal LimitePorGasto { get; set; }
            public decimal PorcentajeMaximoPorGasto { get; set; } = 2.5m;
            public decimal PorcentajeAlertaReposicion { get; set; } = 30m;
            public string? CustodioId { get; set; }

            public decimal EquivalenteEnPesos => MontoFijo * (PorcentajeMaximoPorGasto / 100m);

            public void Limpiar()
            {
                MontoFijo = 0;
                LimitePorGasto = 0;
                PorcentajeMaximoPorGasto = 2.5m;
                PorcentajeAlertaReposicion = 30m;
                CustodioId = null;
            }
        }

        private sealed class EntradaEdicionFondo
        {
            public decimal LimitePorGasto { get; set; }
            public decimal PorcentajeMaximoPorGasto { get; set; }
            public decimal PorcentajeAlertaReposicion { get; set; }
            public string CustodioId { get; set; } = string.Empty;
            public EstadoFondo Estado { get; set; }
        }
    }
}
