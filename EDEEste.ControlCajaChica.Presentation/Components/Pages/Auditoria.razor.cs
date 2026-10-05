using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Application.DTOs;
using EDEEste.ControlCajaChica.Application.Features.Auditoria;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Presentation.Common;
using Microsoft.AspNetCore.Components;

namespace EDEEste.ControlCajaChica.Presentation.Components.Pages
{
    public partial class Auditoria
    {
        private const int EntradasPorPagina = 25;

        // Lo que se muestra de un Id largo; el completo va en el title de la celda.
        private const int LargoCodigoCorto = 8;

        /* TipoAccion es el EntityState con el que AuditoriaInterceptor registró el
           cambio (ConstruirLog: estadoOriginal.ToString()). "Deleted" es un borrado
           lógico: la fila sigue existiendo, marcada IsDeleted. */
        private static readonly IReadOnlyDictionary<string, string> EtiquetasAccion = new Dictionary<string, string>
        {
            ["Added"] = "Creación",
            ["Modified"] = "Modificación",
            ["Deleted"] = "Eliminación"
        };

        // Nombres legibles para las tablas que tienen un nombre técnico o compuesto.
        // Las que no estén aquí se muestran tal cual: ya son legibles.
        private static readonly IReadOnlyDictionary<string, string> NombresDeTabla = new Dictionary<string, string>
        {
            ["CategoriasGasto"] = "Categorías de gasto",
            ["DetallesArqueo"] = "Detalles de arqueo",
            ["SolicitudesPasswordReset"] = "Restablecimientos de contraseña",
            ["AspNetUsers"] = "Usuarios",
            ["AspNetUserRoles"] = "Roles de usuario",
            ["AspNetRoles"] = "Roles"
        };

        [Inject]
        private IBitacoraRepository RepositorioBitacora { get; set; } = default!;

        [Inject]
        private IIdentityService Identidad { get; set; } = default!;

        [Inject]
        private VerificarCadenaBitacoraHandler HandlerCadena { get; set; } = default!;

        [Inject]
        private ListarFirmasInvalidasHandler HandlerFirmas { get; set; } = default!;

        [Inject]
        private ListarRegistrosDesaparecidosHandler HandlerDesaparecidos { get; set; } = default!;

        private enum Pestana
        {
            Bitacora,
            Integridad
        }

        private Pestana pestana = Pestana.Bitacora;

        // UsuarioId de la bitácora → nombre de usuario. "Sistema" no es un Id y se
        // muestra tal cual.
        private readonly Dictionary<string, string> nombresDeUsuario = new();

        private readonly List<string> errores = new();

        // ── Bitácora ─────────────────────────────────────────────────────────────

        private OpcionesFiltroBitacoraDto? opciones;
        private Pagina<LogAuditoria>? pagina;
        private Guid? entradaAbierta;

        private string filtroUsuario = string.Empty;
        private string filtroTabla = string.Empty;
        private string filtroAccion = string.Empty;
        private DateTime? filtroDesde;
        private DateTime? filtroHasta;
        private string filtroRegistro = string.Empty;

        // ── Integridad ───────────────────────────────────────────────────────────

        private string huellaAnterior = string.Empty;
        private bool revisando;
        private DateTime? revisadoEl;
        private ResultadoVerificacionCadena? cadena;
        private IReadOnlyList<RegistroConFirmaInvalidaDto>? firmasInvalidas;
        private IReadOnlyList<MencionEnBitacoraDto>? desaparecidos;

        protected override async Task OnInitializedAsync()
        {
            opciones = await RepositorioBitacora.ListarOpcionesDeFiltroAsync();
            await ResolverNombresAsync(opciones.UsuarioIds);
            await CargarPaginaAsync(1);
        }

        private void CambiarPestana(Pestana nueva)
        {
            errores.Clear();
            pestana = nueva;
        }

        // ── Bitácora: consulta ───────────────────────────────────────────────────

        private async Task CargarPaginaAsync(int numero)
        {
            pagina = await RepositorioBitacora.ListarPaginaAsync(ConstruirFiltro(), numero, EntradasPorPagina);
            entradaAbierta = null;
            await ResolverNombresAsync(pagina.Elementos.Select(l => l.UsuarioId));
        }

        // Cualquier cambio de filtro vuelve a la página 1: la página en la que se
        // estaba casi nunca significa lo mismo con el resultado nuevo.
        private Task AplicarFiltrosAsync() => CargarPaginaAsync(1);

        private Task LimpiarFiltrosAsync()
        {
            filtroUsuario = string.Empty;
            filtroTabla = string.Empty;
            filtroAccion = string.Empty;
            filtroDesde = null;
            filtroHasta = null;
            filtroRegistro = string.Empty;
            return CargarPaginaAsync(1);
        }

        private bool HayFiltros =>
            filtroUsuario.Length > 0 || filtroTabla.Length > 0 || filtroAccion.Length > 0
            || filtroDesde is not null || filtroHasta is not null || filtroRegistro.Trim().Length > 0;

        /* Las fechas se eligen en hora local y la bitácora guarda UTC. "Hasta" incluye
           el día completo: el filtro lo recibe como el inicio del día siguiente
           (exclusivo). */
        private FiltroBitacora ConstruirFiltro() => new()
        {
            UsuarioId = Vacio(filtroUsuario),
            NombreTabla = Vacio(filtroTabla),
            TipoAccion = Vacio(filtroAccion),
            Desde = filtroDesde is { } desde ? InicioDelDiaEnUtc(desde) : null,
            Hasta = filtroHasta is { } hasta ? InicioDelDiaEnUtc(hasta.AddDays(1)) : null,
            RegistroId = Vacio(filtroRegistro)
        };

        private static string? Vacio(string valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

        private static DateTime InicioDelDiaEnUtc(DateTime fechaLocal) =>
            DateTime.SpecifyKind(fechaLocal.Date, DateTimeKind.Local).ToUniversalTime();

        private void AlternarEntrada(Guid id) => entradaAbierta = entradaAbierta == id ? null : id;

        // ── Integridad ───────────────────────────────────────────────────────────

        /// <summary>
        /// Las tres revisiones, una detrás de otra y no en paralelo: comparten el
        /// DbContext del circuito, que no admite dos consultas a la vez. Una que falla
        /// no impide ver el resultado de las otras.
        /// </summary>
        private async Task RevisarIntegridadAsync()
        {
            errores.Clear();
            revisando = true;
            cadena = null;
            firmasInvalidas = null;
            desaparecidos = null;

            try
            {
                var resultadoCadena = await HandlerCadena.EjecutarAsync(
                    new VerificarCadenaBitacoraCommand { HuellaAnterior = huellaAnterior });
                if (resultadoCadena.Exitoso)
                {
                    cadena = resultadoCadena.Valor;
                }
                else
                {
                    errores.AddRange(resultadoCadena.Errores);
                }

                var resultadoFirmas = await HandlerFirmas.EjecutarAsync();
                if (resultadoFirmas.Exitoso)
                {
                    firmasInvalidas = resultadoFirmas.Valor;
                }
                else
                {
                    errores.AddRange(resultadoFirmas.Errores);
                }

                var resultadoDesaparecidos = await HandlerDesaparecidos.EjecutarAsync();
                if (resultadoDesaparecidos.Exitoso)
                {
                    desaparecidos = resultadoDesaparecidos.Valor;
                    await ResolverNombresAsync(desaparecidos!.Select(d => d.UsuarioId));
                }
                else
                {
                    errores.AddRange(resultadoDesaparecidos.Errores);
                }

                revisadoEl = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                errores.Add(ex.Message);
            }
            finally
            {
                revisando = false;
            }
        }

        private static string DescripcionRuptura(TipoRupturaCadena tipo) => tipo switch
        {
            TipoRupturaCadena.FirmaInvalida => "La fila fue editada después de escribirse.",
            TipoRupturaCadena.EnlaceRoto => "Faltan filas justo antes de esta (borradas o reordenadas).",
            _ => tipo.ToString()
        };

        // ── Nombres y formato ────────────────────────────────────────────────────

        private async Task ResolverNombresAsync(IEnumerable<string> ids)
        {
            var pendientes = ids
                .Where(id => !string.IsNullOrEmpty(id) && !nombresDeUsuario.ContainsKey(id))
                .Distinct()
                .ToList();

            if (pendientes.Count == 0)
            {
                return;
            }

            var resueltos = await Identidad.ObtenerNombresUsuarioAsync(pendientes);
            foreach (var id in pendientes)
            {
                nombresDeUsuario[id] = resueltos.GetValueOrDefault(id, id);
            }
        }

        private string NombreUsuario(string usuarioId) =>
            string.IsNullOrEmpty(usuarioId) ? "-" : nombresDeUsuario.GetValueOrDefault(usuarioId, usuarioId);

        private static string NombreTabla(string tabla) => NombresDeTabla.GetValueOrDefault(tabla, tabla);

        private static string EtiquetaAccion(string accion) => EtiquetasAccion.GetValueOrDefault(accion, accion);

        private static string ClaseAccion(string accion) => accion switch
        {
            "Added" => "aud-p-ok",
            "Deleted" => "aud-p-bad",
            _ => "aud-p-info"
        };

        private static string CodigoCorto(string id) => id.Length > LargoCodigoCorto ? id[..LargoCodigoCorto] : id;

        private static string FormatoFechaHora(DateTime fechaUtc)
        {
            var local = fechaUtc.ToLocalTime();
            return local.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) + " " + FormatoHora.HoraCorta(local);
        }

        private static string FormatoFechaHoraOpcional(DateTime? fechaUtc) =>
            fechaUtc is { } valor ? FormatoFechaHora(valor) : "-";
    }
}
