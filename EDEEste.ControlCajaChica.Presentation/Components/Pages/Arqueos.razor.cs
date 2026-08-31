using System;
using System.Collections.Generic;
using System.Globalization;
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

        [Inject]
        private ICurrentUserService UsuarioActual { get; set; } = default!;

        private IReadOnlyList<FondoCajaChica>? fondos;
        private FondoCajaChica? fondoActual;
        private Guid fondoSeleccionado;

        // Los Id de Identity son GUID, no nombres: sin este mapa, el desplegable de
        // fondos y el "Realizado por" del modal mostrarian el GUID crudo.
        private readonly Dictionary<string, string> nombresDeUsuario = new();

        private IReadOnlyList<Gasto>? pendientes;
        private IReadOnlyList<ArqueoCaja>? historial;
        private ArqueoCaja? ultimoArqueo;
        private Guid? arqueoExpandido;

        /// <summary>
        /// §5.1: el conteo arranca desplegado para quien puede capturarlo. Quien no
        /// tiene el permiso nunca llega hasta aqui — su rama del AuthorizeView pinta
        /// la franja con candado, sin estado que alternar.
        /// </summary>
        private bool seccionAbierta = true;

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
            fondos = await FondosVisiblesAsync();
            await ResolverNombresAsync(fondos.Select(f => f.CustodioId));

            if (fondos.Count > 0)
            {
                fondoSeleccionado = fondos[0].Id;
                await CargarFondoAsync(fondoSeleccionado);
            }
        }

        /// <summary>
        /// Un Custodio solo debe poder arquear el fondo que tiene a cargo: sin este
        /// filtro, el desplegable le dejaba elegir el fondo de cualquier otro
        /// custodio. Los demas roles (Gerente, Auditor) conservan la vista completa.
        /// </summary>
        private async Task<IReadOnlyList<FondoCajaChica>> FondosVisiblesAsync()
        {
            var todos = await Fondos.ListarAsync();
            var usuario = await UsuarioActual.ObtenerAsync();

            if (usuario.Id is not { } usuarioId || !await Identidad.EstaEnRolAsync(usuarioId, RolesApp.Custodio))
            {
                return todos;
            }

            return todos.Where(f => f.CustodioId == usuarioId).ToList();
        }

        /// <summary>
        /// Resuelve solo los Id que aun no estan en el mapa: la pantalla vuelve a
        /// pedir nombres cada vez que se cambia de fondo o se registra un arqueo, y
        /// casi siempre son los mismos usuarios.
        /// </summary>
        private async Task ResolverNombresAsync(IEnumerable<string> ids)
        {
            foreach (var id in ids.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct())
            {
                if (!nombresDeUsuario.ContainsKey(id))
                {
                    nombresDeUsuario[id] = await Identidad.ObtenerNombreUsuarioAsync(id) ?? id;
                }
            }
        }

        private string NombreUsuario(string usuarioId) =>
            string.IsNullOrWhiteSpace(usuarioId) ? "(sin asignar)" : nombresDeUsuario.GetValueOrDefault(usuarioId, usuarioId);

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

            await ResolverNombresAsync(historial.Select(a => a.RealizadoPorUsuarioId));
        }

        private void AlternarSeccion() => seccionAbierta = !seccionAbierta;

        private IEnumerable<FilaConteo> FilasBilletes =>
            filas.Where(f => DenominacionesRD.Billetes.Contains(f.ValorDenominacion));

        private IEnumerable<FilaConteo> FilasMonedas =>
            filas.Where(f => DenominacionesRD.Monedas.Contains(f.ValorDenominacion));

        private decimal MontoComprobantesPendientes => pendientes?.Sum(g => g.MontoTotal) ?? 0m;

        private decimal MontoContado => filas.Sum(f => f.Subtotal);

        private decimal SaldoTeorico => fondoActual?.BalanceActual ?? 0m;

        private decimal Diferencia => MontoContado - SaldoTeorico;

        /// <summary>
        /// El mismo criterio que aplica <c>RegistrarArqueoMensualHandler</c>, calculado
        /// en vivo para que la franja y la caja de resultado digan de antemano lo que
        /// va a quedar registrado.
        /// </summary>
        private ResultadoArqueo ResultadoActual => Diferencia switch
        {
            0m => ResultadoArqueo.Cuadrado,
            > 0m => ResultadoArqueo.Sobrante,
            _ => ResultadoArqueo.Faltante
        };

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

        private static void Ajustar(FilaConteo fila, int delta) =>
            fila.Cantidad = Math.Max(0, fila.Cantidad + delta);

        private static void FijarCantidad(FilaConteo fila, string? valor) =>
            fila.Cantidad = int.TryParse(valor, out var cantidad) ? Math.Max(0, cantidad) : 0;

        /// <summary>
        /// La ficha de cada denominacion se va destinando de mayor a menor, para que
        /// el orden del conteo se lea tambien por el peso del color y no solo por el
        /// numero. Los valores salen de DenominacionesRD, asi que un billete nuevo
        /// entra en la escala sin tocar esta pantalla.
        /// </summary>
        private static string OpacidadBillete(decimal valor) =>
            Escalar(DenominacionesRD.Billetes.ToList().IndexOf(valor), DenominacionesRD.Billetes.Count, 0.17, 0.05);

        private static string OpacidadMoneda(decimal valor) =>
            Escalar(DenominacionesRD.Monedas.ToList().IndexOf(valor), DenominacionesRD.Monedas.Count, 0.30, 0.12);

        private static string Escalar(int indice, int total, double maximo, double minimo)
        {
            if (indice < 0 || total <= 1)
            {
                return maximo.ToString("0.###", CultureInfo.InvariantCulture);
            }

            var opacidad = maximo - (maximo - minimo) * indice / (total - 1);
            return opacidad.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private void AlternarDetalle(Guid arqueoId) =>
            arqueoExpandido = arqueoExpandido == arqueoId ? null : arqueoId;

        private void VerObservacion(Guid arqueoId) => arqueoObservacionAbierta = arqueoId;

        private void CerrarObservacion() => arqueoObservacionAbierta = null;

        private ArqueoCaja? ArqueoConObservacionAbierta =>
            arqueoObservacionAbierta is null
                ? null
                : historial?.FirstOrDefault(a => a.Id == arqueoObservacionAbierta);

        /// <summary>
        /// Recorta el texto para que una observacion larga no deforme la tabla del
        /// historial. El texto completo queda a un clic de distancia en el modal.
        /// </summary>
        private static string TruncarObservacion(string? observaciones) =>
            string.IsNullOrWhiteSpace(observaciones)
                ? "—"
                : ObservacionTruncada(observaciones)
                    ? observaciones[..LongitudObservacionEnTabla] + "…"
                    : observaciones;

        private static bool ObservacionTruncada(string? observaciones) =>
            !string.IsNullOrWhiteSpace(observaciones) && observaciones.Length > LongitudObservacionEnTabla;

        /// <summary>
        /// Signo explicito en las dos direcciones: un sobrante con "+" delante se
        /// distingue de un faltante aunque el lector no repare en el color.
        /// </summary>
        private static string FormatoDiferencia(decimal diferencia) => diferencia switch
        {
            0m => "0.00",
            > 0m => "+" + diferencia.ToString("N2"),
            _ => "−" + Math.Abs(diferencia).ToString("N2")
        };

        private static string ClaseBadge(ResultadoArqueo resultado) => resultado switch
        {
            ResultadoArqueo.Cuadrado => "arq-bdg-ok",
            ResultadoArqueo.Sobrante => "arq-bdg-up",
            _ => "arq-bdg-bad"
        };

        private static string ClaseCajaResultado(ResultadoArqueo resultado) => resultado switch
        {
            ResultadoArqueo.Cuadrado => "arq-verdict-ok",
            ResultadoArqueo.Sobrante => "arq-verdict-up",
            _ => "arq-verdict-bad"
        };

        private static string ClaseTextoResultado(ResultadoArqueo resultado) => resultado switch
        {
            ResultadoArqueo.Cuadrado => "arq-txt-ok",
            ResultadoArqueo.Sobrante => "arq-txt-up",
            _ => "arq-txt-bad"
        };

        /// <summary>
        /// Variante aclarada para el degradado oscuro del modal (DESIGN.md §5.3): es la
        /// unica excepcion a no tocar los colores de estado, y solo aplica a texto
        /// suelto — los badges conservan su aspecto claro tal cual.
        /// </summary>
        private static string ClaseTextoResultadoOscuro(ResultadoArqueo resultado) => resultado switch
        {
            ResultadoArqueo.Cuadrado => "arq-txt-ok-dark",
            ResultadoArqueo.Sobrante => "arq-txt-up-dark",
            _ => "arq-txt-bad-dark"
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

                // Registrado el conteo, lo que interesa es el historial: la franja
                // plegada conserva el resumen y deja la lista servida completa.
                seccionAbierta = false;
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
