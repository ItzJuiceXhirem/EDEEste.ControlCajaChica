using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Features.Gastos;
using EDEEste.ControlCajaChica.Domain.Constants;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace EDEEste.ControlCajaChica.Presentation.Components.Pages
{
    public partial class Gastos
    {
        private const int LongitudMotivoEnTabla = 60;

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

        [Inject]
        private IIdentityService Identidad { get; set; } = default!;

        [Inject]
        private ICurrentUserService UsuarioActual { get; set; } = default!;

        [Inject]
        private IJSRuntime JsRuntime { get; set; } = default!;

        // Referencias para posicionar la ficha (§5.9) al lado de la fila que la abrió,
        // en vez de que aparezca siempre arriba del todo de la tabla. Un Dictionary y
        // no una sola ElementReference porque el @ref vive dentro de un @foreach: cada
        // fila necesita la suya para poder medir la que el usuario tocó.
        private ElementReference tarjetaMovimientosRef;
        private ElementReference fichaRef;
        private readonly Dictionary<Guid, ElementReference> filaRefs = new();

        // Evita reposicionar en cada render (p. ej. al teclear en el buscador de la
        // ficha, si lo hubiera): solo hace falta recalcular cuando cambia CUAL fila
        // está abierta.
        private Guid? fichaPosicionadaPara;

        private IReadOnlyList<FondoCajaChica>? fondos;

        // Los Id de Identity son GUID, no nombres: sin este mapa, el desplegable de
        // fondos y el "registrado por" de la ficha mostrarian el GUID crudo.
        private readonly Dictionary<string, string> nombresDeUsuario = new();

        private IReadOnlyList<Gasto>? gastos;
        private IReadOnlyList<Gasto>? anulacionesPendientes;
        private IReadOnlyList<Gasto>? anulados;
        private Guid fondoSeleccionado;

        // null = "Todos". Filtra en memoria sobre lo ya cargado (ver GastosFiltrados).
        private EstadoGasto? filtroEstado;

        /// <summary>
        /// §5.8: la cifra de "Anulado" no filtra la tabla de movimientos — abre una
        /// vista distinta, porque una anulacion trae columnas propias (motivo, quien
        /// decide) que no encajan en las de un gasto pendiente.
        /// </summary>
        private bool vistaAnulados;

        /// <summary>§5.9: fila abierta en la ficha lateral.</summary>
        private Guid? gastoSeleccionado;

        private string busquedaMovimientos = string.Empty;

        // null = todo el historial.
        private int? diasAnulados = 90;
        private string busquedaAnulados = string.Empty;

        // Ventana de días en Movimientos: visible tanto en "Todos" (sin cifra
        // seleccionada) como en "Repuesto" -- las otras dos cifras son colas activas
        // y no un historial que crezca sin límite. Arranca en null (todo el
        // historial): a diferencia de Anulados, aquí no hay una consulta al
        // repositorio que se acote por fecha -- el filtro es en memoria sobre
        // "gastos", que ya está completo -- así que empezar mostrando todo es lo que
        // menos sorprende.
        private int? diasMovimientos;

        // Solo guarda el Id: el texto se lee de la lista al pintar el modal, para no
        // duplicar el estado.
        private Guid? motivoAbierto;

        // Gasto cuyo formulario de motivo esta abierto, y el motivo que se esta
        // escribiendo. anulacionDirectaEnCurso distingue si es "Solicitar anulacion"
        // (Custodio, no toca el balance) o "Anular" directo (Gerente, si lo toca) --
        // mismo formulario, pero el boton de confirmar llama a un handler distinto.
        private Guid? gastoEnAnulacion;
        private bool anulacionDirectaEnCurso;
        private string motivoAnulacion = string.Empty;

        // Bloquea todos los botones de accion mientras se procesa un gasto.
        private Guid? procesando;

        private readonly List<string> errores = new();
        private string? exito;

        private string Lede => vistaAnulados
            ? "Mostrando anulaciones: lo que espera decisión y el historial de lo ya anulado."
            : "Registre y dé seguimiento a los gastos del fondo hasta su reposición.";

        protected override async Task OnInitializedAsync()
        {
            fondos = await FondosVisiblesAsync();
            await ResolverNombresAsync(fondos.Select(f => f.CustodioId));

            if (fondos.Count > 0)
            {
                fondoSeleccionado = fondos[0].Id;
                await CargarTodoAsync(fondoSeleccionado);
            }
        }

        /// <summary>
        /// Un Custodio solo debe ver el fondo que tiene a cargo: sin este filtro,
        /// el desplegable le dejaba elegir el fondo de cualquier otro custodio y ver
        /// sus gastos y comprobantes. Los demas roles con VerGastos (Gerente,
        /// Finanzas, Auditor, Administrador) conservan la vista sin restringir.
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
        /// Posiciona la ficha para que su parte superior arranque a la altura de la
        /// fila que la abrió, en vez de siempre arriba del todo de la tarjeta -- con
        /// una tabla larga, abrir una fila del final obligaba a subir para ver la
        /// ficha. Si la fila está cerca del final, el borde inferior de la ficha se
        /// alinea con el borde inferior de la fila en su lugar, para que la ficha
        /// nunca sobresalga por debajo de la tarjeta de movimientos.
        ///
        /// Se hace por JS y no con estado en C# porque depende de un alto que solo el
        /// navegador conoce (el de la ficha ya renderizada) -- medirlo y aplicarlo en
        /// la misma llamada evita un segundo viaje al servidor solo para reposicionar.
        /// </summary>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (gastoSeleccionado == fichaPosicionadaPara)
            {
                return;
            }

            fichaPosicionadaPara = gastoSeleccionado;

            if (gastoSeleccionado is not { } id || !filaRefs.TryGetValue(id, out var filaRef))
            {
                return;
            }

            await JsRuntime.InvokeVoidAsync("ccGastosFicha.posicionar", tarjetaMovimientosRef, filaRef, fichaRef);
        }

        /// <summary>
        /// Resuelve solo los Id que aun no estan en el mapa: la pantalla vuelve a
        /// pedir nombres cada vez que se cambia de fondo o se anula un gasto, y casi
        /// siempre son los mismos usuarios.
        /// </summary>
        private async Task ResolverNombresAsync(IEnumerable<string> ids)
        {
            var pendientes = ids.Where(id => !string.IsNullOrWhiteSpace(id) && !nombresDeUsuario.ContainsKey(id))
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
            string.IsNullOrWhiteSpace(usuarioId) ? "(sin asignar)" : nombresDeUsuario.GetValueOrDefault(usuarioId, usuarioId);

        private async Task CambiarFondoAsync(ChangeEventArgs e)
        {
            if (Guid.TryParse(e.Value?.ToString(), out var id))
            {
                errores.Clear();
                exito = null;
                fondoSeleccionado = id;
                CerrarFicha();
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

            await ResolverNombresAsync(gastos.Select(g => g.RegistradoPorUsuarioId));
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

        // Sin await: a diferencia de Anulados, Movimientos no tiene una consulta
        // propia al repositorio -- filtra en memoria sobre "gastos" (GastosFiltrados).
        private void CambiarVentanaMovimientos(ChangeEventArgs e)
        {
            var valor = e.Value?.ToString();
            diasMovimientos = string.IsNullOrEmpty(valor) ? null : int.Parse(valor, CultureInfo.InvariantCulture);
        }

        // ── Cifras de resumen (§5.8) ─────────────────────────────────────────────

        private decimal Total(EstadoGasto estado) =>
            gastos?.Where(g => g.Estado == estado).Sum(g => g.MontoTotal) ?? 0m;

        private int Conteo(EstadoGasto estado) =>
            gastos?.Count(g => g.Estado == estado) ?? 0;

        private decimal TotalAnulados => anulados?.Sum(g => g.MontoTotal) ?? 0m;

        private string VentanaAnulados => diasAnulados is { } dias ? $"· {dias} días" : "· histórico";

        private bool EstadoActivo(EstadoGasto estado) => !vistaAnulados && filtroEstado == estado;

        /// <summary>
        /// La ventana de días de Movimientos sólo tiene sentido en "Todos" (sin cifra
        /// seleccionada) y en "Repuesto": las otras dos cifras (Pendiente, En proceso)
        /// son colas de trabajo activas, no un historial que acumule con el tiempo.
        /// </summary>
        private bool MuestraVentanaDeDias => filtroEstado is null || filtroEstado == EstadoGasto.Repuesto;

        /// <summary>
        /// La misma cifra enciende y apaga su filtro: volver a pulsarla devuelve la
        /// tabla completa, sin necesidad de una opcion "Todos" aparte.
        /// </summary>
        private void FiltrarPor(EstadoGasto estado)
        {
            vistaAnulados = false;
            filtroEstado = filtroEstado == estado ? null : estado;
            CerrarFicha();
        }

        private void AbrirAnulados()
        {
            vistaAnulados = !vistaAnulados;
            filtroEstado = null;
            CerrarFicha();
        }

        // ── Ficha lateral (§5.9) ─────────────────────────────────────────────────

        /// <summary>
        /// La fila abierta puede venir de Movimientos, de "Solicitudes por confirmar"
        /// o de "Gastos anulados": las tres tienen filas clickeables (§5.9), así que
        /// se busca en las tres listas cargadas.
        /// </summary>
        private Gasto? GastoEnFicha
        {
            get
            {
                if (gastoSeleccionado is not { } id)
                {
                    return null;
                }

                return gastos?.FirstOrDefault(g => g.Id == id)
                    ?? anulacionesPendientes?.FirstOrDefault(g => g.Id == id)
                    ?? anulados?.FirstOrDefault(g => g.Id == id);
            }
        }

        /// <summary>
        /// La misma fila abre y cierra la ficha; el boton ✕ de la cabecera hace lo
        /// mismo. Los dos mecanismos coexisten a proposito (§5.9).
        /// </summary>
        private void AlternarFicha(Guid gastoId)
        {
            if (gastoSeleccionado == gastoId)
            {
                CerrarFicha();
                return;
            }

            gastoSeleccionado = gastoId;
            CancelarFormularioAnulacion();
        }

        private void CerrarFicha()
        {
            gastoSeleccionado = null;
            CancelarFormularioAnulacion();
        }

        // ── Filtros en memoria ───────────────────────────────────────────────────

        /// <summary>
        /// Filtra en memoria la tabla principal; no vuelve a la BDD porque
        /// ListarPorFondoAsync ya trajo todos los gastos del fondo de una vez.
        /// </summary>
        private IEnumerable<Gasto> GastosFiltrados
        {
            get
            {
                if (gastos is null)
                {
                    return [];
                }

                var filtrados = filtroEstado is null ? gastos : gastos.Where(g => g.Estado == filtroEstado);

                // La ventana de días aplica en "Todos" (sin cifra) y en "Repuesto" --
                // las otras dos cifras son colas activas, no un historial que crezca
                // sin límite (ver MuestraVentanaDeDias). Filtra por FechaGasto -- la
                // fecha del gasto en si, no FechaModificacion (esa es cuando se tocó
                // el registro por última vez, y casi todos los gastos de prueba se
                // modificaron hace poco sin importar de qué fecha era el gasto).
                // DateTime.Today y no UtcNow: FechaGasto es una fecha de calendario
                // sin componente de hora que importe.
                if (MuestraVentanaDeDias && diasMovimientos is { } dias)
                {
                    var desde = DateTime.Today.AddDays(-dias);
                    filtrados = filtrados.Where(g => g.FechaGasto >= desde);
                }

                return string.IsNullOrWhiteSpace(busquedaMovimientos)
                    ? filtrados
                    : filtrados.Where(g => Coincide(g, busquedaMovimientos.Trim()));
            }
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
                gasto.CategoriaGasto?.Nombre,
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

        // ── Motivo: truncado y modal (§5.2 y §5.3) ───────────────────────────────

        private static bool MotivoTruncado(string? motivo) =>
            !string.IsNullOrWhiteSpace(motivo) && motivo.Length > LongitudMotivoEnTabla;

        private static string TruncarMotivo(string? motivo) =>
            string.IsNullOrWhiteSpace(motivo)
                ? "—"
                : MotivoTruncado(motivo)
                    ? motivo[..LongitudMotivoEnTabla] + "…"
                    : motivo;

        private void VerMotivo(Guid gastoId) => motivoAbierto = gastoId;

        private void CerrarMotivo() => motivoAbierto = null;

        /// <summary>
        /// El gasto puede estar en cualquiera de las dos listas de la vista de
        /// anulaciones, asi que se busca en ambas.
        /// </summary>
        private Gasto? GastoConMotivoAbierto =>
            motivoAbierto is null
                ? null
                : anulacionesPendientes?.FirstOrDefault(g => g.Id == motivoAbierto)
                  ?? anulados?.FirstOrDefault(g => g.Id == motivoAbierto);

        // ── Estados ──────────────────────────────────────────────────────────────

        /// <summary>
        /// §5.7: pildora con fondo tenue, no la variante solida de Bootstrap. Los
        /// hues siguen respondiendo a la semantica de §1.4 — cambia la construccion
        /// del badge, no que color significa que.
        /// </summary>
        private static string ClaseEstado(EstadoGasto estado) => estado switch
        {
            EstadoGasto.PendienteReposicion => "gst-p-warn",
            EstadoGasto.EnProcesoReposicion => "gst-p-info",
            EstadoGasto.Repuesto => "gst-p-ok",
            EstadoGasto.Rechazado or EstadoGasto.Anulado => "gst-p-bad",
            EstadoGasto.AnulacionPendiente => "gst-p-dark",
            _ => "gst-p-dark"
        };

        private static string EtiquetaEstado(EstadoGasto estado) => estado switch
        {
            EstadoGasto.PendienteReposicion => "Pendiente",
            EstadoGasto.EnProcesoReposicion => "En proceso",
            EstadoGasto.Repuesto => "Repuesto",
            EstadoGasto.Rechazado => "Rechazado",
            EstadoGasto.Anulado => "Anulado",
            EstadoGasto.AnulacionPendiente => "Anulación pendiente",
            _ => estado.ToString()
        };

        // ── Anulación ────────────────────────────────────────────────────────────

        /// <summary>
        /// Abrir el formulario tambien abre la ficha: el motivo se escribe alli, no
        /// en una fila expandida de la tabla.
        /// </summary>
        private void AbrirFormularioAnulacion(Guid gastoId, bool directa)
        {
            gastoSeleccionado = gastoId;
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
                CerrarFicha();
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
                CerrarFicha();
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
