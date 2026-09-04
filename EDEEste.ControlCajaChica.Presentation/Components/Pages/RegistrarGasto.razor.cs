using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Features.Gastos;
using EDEEste.ControlCajaChica.Domain.Constants;
using EDEEste.ControlCajaChica.Domain.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace EDEEste.ControlCajaChica.Presentation.Components.Pages
{
    public partial class RegistrarGasto
    {
        [Inject]
        private IFondoRepository Fondos { get; set; } = default!;

        [Inject]
        private ICategoriaGastoRepository Categorias { get; set; } = default!;

        [Inject]
        private IIdentityService Identidad { get; set; } = default!;

        [Inject]
        private ICurrentUserService UsuarioActual { get; set; } = default!;

        [Inject]
        private IFileStorageService Almacenamiento { get; set; } = default!;

        [Inject]
        private RegistrarGastoHandler Handler { get; set; } = default!;

        [Inject]
        private IJSRuntime JsRuntime { get; set; } = default!;

        // Sirve el token antiforgery que la subida manda por fetch(): la subida va
        // por HTTP normal (GastoEndpoints), fuera del circuito de Blazor Server, asi
        // que necesita el mismo token que llevaria un <form> comun. Este componente
        // no lo captura del DOM ni lo pide por separado -- es el mismo que ya quedo
        // persistido para todo el circuito al servir la pagina.
        [Inject]
        private AntiforgeryStateProvider Antiforgery { get; set; } = default!;

        // Referencias para conectar el arrastrar-y-soltar sobre la zona de carga: sin
        // esto, soltar un archivo lo abre en una pestaña del navegador en vez de
        // entregarlo al <input type="file"> real que renderiza InputFile.
        private ElementReference zonaDropRef;
        private InputFile? inputFileRef;

        // Falso hasta que el circuito interactivo complete su primer render. La
        // pagina prerenderiza como HTML estatico antes de que el circuito conecte, y
        // en esa ventana el <input type="file"> ya es real y aceptaria un archivo sin
        // que ningun manejador de C# este conectado todavia -- la seleccion se
        // perderia en silencio. OnAfterRenderAsync solo corre dentro de un circuito ya
        // conectado, asi que su primera pasada es la confirmacion de que ya es seguro
        // dejar interactuar con la zona de carga.
        private bool listo;

        private IReadOnlyList<FondoCajaChica>? fondos;
        private IReadOnlyList<CategoriaGasto>? categorias;

        // CustodioId guarda el Id de Identity (un GUID), no un nombre: sin este mapa,
        // el desplegable de fondos mostraria el GUID crudo en vez del nombre de usuario.
        private Dictionary<string, string> nombresDeCustodio = new();

        private readonly EntradaGasto entrada = new();
        private readonly List<AdjuntoSeleccionado> adjuntos = new();
        private readonly List<string> errores = new();

        private bool guardando;
        private string? exito;

        protected override async Task OnInitializedAsync()
        {
            fondos = await FondosVisiblesAsync();
            categorias = await Categorias.ListarActivasAsync();
            await ResolverNombresDeCustodioAsync();

            entrada.FondoCajaChicaId = fondos.FirstOrDefault()?.Id ?? Guid.Empty;
            entrada.CategoriaGastoId = categorias.FirstOrDefault()?.Id ?? Guid.Empty;
        }

        /// <summary>
        /// Un Custodio solo debe poder registrar gastos contra el fondo que tiene a
        /// cargo: sin este filtro, el desplegable le dejaba elegir (y descontar
        /// saldo de) el fondo de cualquier otro custodio. Los demas roles conservan
        /// la vista sin restringir.
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
        /// No se limita a "firstRender" para conectar el arrastrar-y-soltar: mientras
        /// se cargan fondos/categorias la pantalla todavia muestra "Cargando...", asi
        /// que la zona de arrastre (y su InputFile) no existen en ese primer render --
        /// solo aparecen despues, en un render posterior. El propio JS es idempotente
        /// (ver dataset.ccWired en interop.js), asi que llamarlo de mas en renders
        /// subsiguientes no duplica los listeners.
        ///
        /// "listo" si se pone una sola vez en el primer render interactivo -- ver su
        /// comentario -- y de ahi en adelante ya no importa que markup se muestre.
        /// </summary>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                listo = true;
                StateHasChanged();
            }

            if (inputFileRef?.Element is { } elementoInput)
            {
                await JsRuntime.InvokeVoidAsync("ccDragDrop.wire", zonaDropRef, elementoInput);
            }
        }

        /// <summary>
        /// A diferencia de ResolverNombresAsync (Arqueos/Gastos/Reposiciones), este no es
        /// incremental: reconstruye el mapa completo desde <c>fondos</c> en cada llamada, sin
        /// reusar lo ya resuelto. Nombre distinto a proposito -- es una operacion distinta.
        /// </summary>
        private async Task ResolverNombresDeCustodioAsync()
        {
            var ids = fondos!.Select(f => f.CustodioId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
            var resueltos = await Identidad.ObtenerNombresUsuarioAsync(ids);

            var mapa = new Dictionary<string, string>();
            foreach (var id in ids)
            {
                mapa[id] = resueltos.GetValueOrDefault(id, id);
            }

            nombresDeCustodio = mapa;
        }

        private string NombreCustodio(string custodioId) =>
            string.IsNullOrWhiteSpace(custodioId) ? "(sin asignar)" : nombresDeCustodio.GetValueOrDefault(custodioId, custodioId);

        private FondoCajaChica? FondoActual =>
            fondos?.FirstOrDefault(f => f.Id == entrada.FondoCajaChicaId);

        /// <summary>
        /// El total no es un campo del formulario sino la suma de sus dos partes: con
        /// un tercer campo editable, el cuadre Subtotal + ITBIS = Total que valida el
        /// handler podia fallar por una simple errata de tecleo. Aqui no puede.
        /// </summary>
        private decimal MontoTotal => (entrada.Subtotal ?? 0m) + (entrada.MontoITBIS ?? 0m);

        /// <summary>
        /// Misma formula que <c>RegistrarGastoHandler.CalcularLimitePorGasto</c>: el
        /// mas estricto entre el tope porcentual del fondo y su limite absoluto, si lo
        /// hay. Aqui es solo para avisar; quien decide sigue siendo el handler.
        /// </summary>
        private static decimal LimitePorGasto(FondoCajaChica fondo)
        {
            var topeReglamentario = fondo.MontoFijo * (fondo.PorcentajeMaximoPorGasto / 100m);

            return fondo.LimitePorGasto > 0
                ? Math.Min(fondo.LimitePorGasto, topeReglamentario)
                : topeReglamentario;
        }

        private string Encabezado
        {
            get
            {
                if (FondoActual is not { } fondo)
                {
                    return "Complete los datos de la factura y adjunte sus comprobantes.";
                }

                return $"Tope por gasto RD$ {LimitePorGasto(fondo):N2} · disponible RD$ {fondo.BalanceActual:N2}";
            }
        }

        /// <summary>
        /// Aviso en vivo del tope, para que el rechazo no llegue recien al guardar.
        /// Solo aparece con un monto escrito: en blanco no hay nada que avisar.
        /// </summary>
        private AvisoDeTope? AvisoTope
        {
            get
            {
                if (FondoActual is not { } fondo || MontoTotal <= 0m)
                {
                    return null;
                }

                var limite = LimitePorGasto(fondo);
                var margen = limite - MontoTotal;

                return margen >= 0m
                    ? new AvisoDeTope(true,
                        $"Dentro del tope de RD$ {limite:N2} por gasto, por RD$ {margen:N2}. " +
                        $"El fondo queda en RD$ {fondo.BalanceActual - MontoTotal:N2}.")
                    : new AvisoDeTope(false,
                        $"Se pasa del tope de RD$ {limite:N2} por gasto por RD$ {Math.Abs(margen):N2}. " +
                        "El registro será rechazado.");
            }
        }

        private sealed record AvisoDeTope(bool DentroDelTope, string Mensaje);

        /// <summary>
        /// No lee los bytes del archivo en ningun momento -- eso es justo lo que se
        /// queria sacar del circuito. Arma la fila (metadatos nomas, gratis) y le pide
        /// al JS que suba el <input> real via fetch(); cuando esa promesa resuelve, se
        /// actualiza cada fila con su referencia de staging o su error.
        /// </summary>
        private async Task SeleccionarArchivosAsync(InputFileChangeEventArgs e)
        {
            errores.Clear();

            // Los adjuntos de una seleccion anterior se descartan tambien del lado
            // del servidor: sin esto, cada vez que alguien cambia de opinion sobre
            // que archivos adjuntar, los viejos quedarian huerfanos en staging hasta
            // que los alcance el barrido de 24 horas. Sin esperarlo -- la fila ya sale
            // de la lista sin importar que devuelva el descarte.
            foreach (var previo in adjuntos.Where(a => a.Referencia is not null))
            {
                _ = DescartarSiEsPosibleAsync(previo.Referencia!.Value);
            }

            adjuntos.Clear();

            if (!IntentarObtenerArchivosSeleccionados(e, out var seleccionados))
            {
                errores.Add($"No se pueden adjuntar mas de {LimitesGasto.ComprobantesPorGasto} comprobantes.");
                return;
            }

            foreach (var archivo in seleccionados)
            {
                adjuntos.Add(new AdjuntoSeleccionado
                {
                    NombreOriginal = archivo.Name,
                    TamanoBytes = archivo.Size,
                    Subiendo = true
                });
            }

            if (adjuntos.Count == 0)
            {
                return;
            }

            // Se muestra "Subiendo..." de inmediato; la subida real puede tardar.
            StateHasChanged();

            if (inputFileRef?.Element is not { } elementoInput)
            {
                MarcarErrorEnTodos("No se pudo acceder al selector de archivos.");
                return;
            }

            var token = Antiforgery.GetAntiforgeryToken();
            if (token is null)
            {
                MarcarErrorEnTodos("No se pudo preparar la subida. Recargue la página e intente de nuevo.");
                return;
            }

            ResultadoSubidaJs[] resultados;
            try
            {
                resultados = await JsRuntime.InvokeAsync<ResultadoSubidaJs[]>(
                    "ccSubidas.subir", elementoInput, "/gastos/comprobantes/staging", token.FormFieldName, token.Value);
            }
            catch (JSException)
            {
                MarcarErrorEnTodos("No se pudo conectar con el servidor para subir los archivos.");
                return;
            }

            for (var i = 0; i < adjuntos.Count; i++)
            {
                var adjunto = adjuntos[i];
                adjunto.Subiendo = false;

                var resultado = i < resultados.Length ? resultados[i] : null;
                if (resultado?.Referencia is { } referencia)
                {
                    adjunto.Referencia = referencia;
                }
                else
                {
                    adjunto.Error = resultado?.Error ?? "No se pudo subir el archivo.";
                }
            }
        }

        // Muy por encima de LimitesGasto.ComprobantesPorGasto a proposito: es un
        // margen tecnico para GetMultipleFiles, no el limite de negocio. Ningun
        // selector de archivos normal deberia acercarse a este numero -- el limite
        // real se comprueba aparte, con Count, para poder mostrar un error amigable.
        private const int TechoTecnicoSeleccion = 100;

        /// <summary>
        /// GetMultipleFiles lanza InvalidOperationException si se le pasan mas
        /// archivos que el maximo indicado. Pasarle directamente
        /// LimitesGasto.ComprobantesPorGasto dejaria esa excepcion sin capturar en
        /// cuanto alguien seleccionara un archivo de mas -- y una excepcion sin
        /// capturar en un manejador de evento tumba el circuito de Blazor Server
        /// entero (mismo riesgo que documenta AGENTS.md para una llamada de JS
        /// interop sin try/catch, solo que aqui la llamada peligrosa es sincrona).
        ///
        /// Por eso el techo que se le pasa a GetMultipleFiles es generoso
        /// (TechoTecnicoSeleccion) y el limite real se comprueba aparte, a mano, con
        /// Count -- eso es lo que permite mostrar "no se pueden adjuntar mas de 20"
        /// en vez de perder el formulario. El catch es un respaldo adicional para el
        /// caso extremo de superar incluso el techo tecnico.
        /// </summary>
        private static bool IntentarObtenerArchivosSeleccionados(
            InputFileChangeEventArgs e, out IReadOnlyList<IBrowserFile> archivos)
        {
            try
            {
                archivos = e.GetMultipleFiles(maximumFileCount: TechoTecnicoSeleccion);
            }
            catch (InvalidOperationException)
            {
                archivos = Array.Empty<IBrowserFile>();
                return false;
            }

            return archivos.Count <= LimitesGasto.ComprobantesPorGasto;
        }

        private void MarcarErrorEnTodos(string mensaje)
        {
            foreach (var adjunto in adjuntos)
            {
                adjunto.Subiendo = false;
                adjunto.Error = mensaje;
            }
        }

        private async Task QuitarAdjuntoAsync(AdjuntoSeleccionado adjunto)
        {
            adjuntos.Remove(adjunto);
            errores.Clear();

            if (adjunto.Referencia is { } referencia)
            {
                await DescartarSiEsPosibleAsync(referencia);
            }
        }

        /// <summary>
        /// El descarte llama directo a IFileStorageService y no al endpoint HTTP de
        /// borrado: esta pagina ya corre dentro de un circuito autenticado con
        /// inyeccion de dependencias, asi que salir por HTTP (con su propio token
        /// antiforgery) seria un viaje de mas para lo mismo. El endpoint sigue
        /// existiendo para quien no tenga ese contexto -- por ejemplo, JS puro sin
        /// circuito detras.
        /// </summary>
        private async Task DescartarSiEsPosibleAsync(Guid referencia)
        {
            var usuario = await UsuarioActual.ObtenerAsync();
            if (usuario.Id is { } usuarioId)
            {
                await Almacenamiento.EliminarStagingAsync(usuarioId, referencia);
            }
        }

        /// <summary>
        /// Corre despues de cada pulsacion (via @bind-Value:after). Deja solo dígitos
        /// (descarta letras y símbolos) y, cuando llegan a 11 (una cédula), los
        /// muestra como XXX-XXXXXXX-X. Un RNC de empresa (9 dígitos) se deja sin
        /// guiones. Lo que se envía al handler se limpia de guiones otra vez allí, así
        /// que en BDD solo entran dígitos.
        /// </summary>
        private void FormatearRnc()
        {
            var digitos = new string((entrada.RNCProveedor ?? string.Empty).Where(char.IsDigit).ToArray());

            if (digitos.Length > 11)
            {
                digitos = digitos[..11];
            }

            entrada.RNCProveedor = digitos.Length == 11
                ? $"{digitos[..3]}-{digitos[3..10]}-{digitos[10]}"
                : digitos;
        }

        /// <summary>
        /// Corre despues de cada pulsacion (via @bind-Value:after). Descarta símbolos
        /// y espacios y normaliza a mayúsculas; no fuerza el formato completo mientras
        /// se escribe (un NCF a medio teclear no calza con el patrón todavía). El
        /// handler valida el formato exacto al guardar.
        /// </summary>
        private void FiltrarNcf()
        {
            var texto = (entrada.NCF ?? string.Empty).ToUpperInvariant();
            var filtrado = texto.Where(char.IsLetterOrDigit).ToArray();
            entrada.NCF = new string(filtrado, 0, Math.Min(filtrado.Length, 13));
        }

        private async Task GuardarAsync()
        {
            errores.Clear();
            exito = null;

            if (adjuntos.Any(a => a.Subiendo))
            {
                errores.Add("Espere a que terminen de subir los comprobantes.");
                return;
            }

            if (adjuntos.Any(a => a.Referencia is null))
            {
                errores.Add("Uno o más comprobantes no se pudieron subir. Quítelos o inténtelo de nuevo antes de guardar.");
                return;
            }

            guardando = true;

            try
            {
                var comando = new RegistrarGastoCommand
                {
                    FondoCajaChicaId = entrada.FondoCajaChicaId,
                    CategoriaGastoId = entrada.CategoriaGastoId,
                    Proveedor = entrada.Proveedor ?? string.Empty,
                    RNCProveedor = entrada.RNCProveedor ?? string.Empty,
                    NCF = entrada.NCF ?? string.Empty,
                    Concepto = entrada.Concepto,
                    Subtotal = entrada.Subtotal ?? 0,
                    MontoITBIS = entrada.MontoITBIS ?? 0,
                    MontoTotal = MontoTotal,
                    FechaGasto = entrada.FechaGasto
                };

                foreach (var adjunto in adjuntos)
                {
                    comando.Comprobantes.Add(new RegistrarGastoCommand.ComprobanteEntrada
                    {
                        Referencia = adjunto.Referencia!.Value,
                        Descripcion = adjunto.Descripcion
                    });
                }

                var resultado = await Handler.EjecutarAsync(comando);

                if (!resultado.Exitoso)
                {
                    errores.AddRange(resultado.Errores);

                    // Un fallo de concurrencia deja el balance mostrado desactualizado
                    // (otro usuario ya lo cambió) hasta que alguien recargue a mano; se
                    // releen los fondos aqui mismo para que el mensaje de error y el
                    // balance en pantalla queden consistentes de una vez.
                    fondos = await FondosVisiblesAsync();
                    return;
                }

                exito = "Gasto registrado correctamente.";
                entrada.Limpiar(fondos!.FirstOrDefault()?.Id ?? Guid.Empty, categorias!.FirstOrDefault()?.Id ?? Guid.Empty);
                adjuntos.Clear();

                // El balance del fondo cambió, hay que releerlo para el próximo registro.
                // FondosVisiblesAsync() y no Fondos.ListarAsync(): esta segunda recarga se
                // habia quedado sin el filtro de custodio, y un Custodio volvia a ver
                // todos los fondos del sistema apenas registraba un gasto.
                fondos = await FondosVisiblesAsync();
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

        private sealed class AdjuntoSeleccionado
        {
            public string NombreOriginal { get; set; } = string.Empty;
            public long TamanoBytes { get; set; }
            public string Descripcion { get; set; } = string.Empty;

            // Null mientras sube o si la subida fallo -- ver Subiendo/Error.
            public Guid? Referencia { get; set; }
            public bool Subiendo { get; set; }
            public string? Error { get; set; }
        }

        /// <summary>Forma exacta del objeto que devuelve ccSubidas.subir() en interop.js.</summary>
        private sealed class ResultadoSubidaJs
        {
            public Guid? Referencia { get; set; }
            public string? Error { get; set; }
        }

        private sealed class EntradaGasto
        {
            public Guid FondoCajaChicaId { get; set; }
            public Guid CategoriaGastoId { get; set; }
            public string? Proveedor { get; set; }
            public string? RNCProveedor { get; set; }
            public string? NCF { get; set; }
            public string? Concepto { get; set; }

            // Nullables para que el campo salga vacío y se vea el placeholder "0" en vez
            // de un cero escrito que el usuario tiene que borrar antes de teclear.
            //
            // No hay MontoTotal: es la suma de estos dos y se calcula en la pantalla
            // (ver RegistrarGasto.MontoTotal). Al no existir como campo, el cuadre no
            // puede romperse por una errata.
            public decimal? Subtotal { get; set; }
            public decimal? MontoITBIS { get; set; }
            public DateTime FechaGasto { get; set; } = DateTime.Today;

            public void Limpiar(Guid fondoId, Guid categoriaId)
            {
                FondoCajaChicaId = fondoId;
                CategoriaGastoId = categoriaId;
                Proveedor = null;
                RNCProveedor = null;
                NCF = null;
                Concepto = null;
                Subtotal = null;
                MontoITBIS = null;
                FechaGasto = DateTime.Today;
            }
        }
    }
}
