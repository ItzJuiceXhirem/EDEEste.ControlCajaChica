using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Features.Gastos;
using EDEEste.ControlCajaChica.Domain.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace EDEEste.ControlCajaChica.Presentation.Components.Pages
{
    public partial class RegistrarGasto
    {
        // Los adjuntos de una factura son fotos o PDF de pocas páginas; 10 MB deja
        // holgura de sobra y evita que el límite de 512 KB que trae InputFile por
        // defecto corte una foto de celular.
        private const long TamanoMaximoArchivo = 10 * 1024 * 1024;

        [Inject]
        private IFondoRepository Fondos { get; set; } = default!;

        [Inject]
        private ICategoriaGastoRepository Categorias { get; set; } = default!;

        [Inject]
        private IIdentityService Identidad { get; set; } = default!;

        [Inject]
        private RegistrarGastoHandler Handler { get; set; } = default!;

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
            fondos = await Fondos.ListarAsync();
            categorias = await Categorias.ListarActivasAsync();
            await ResolverNombresDeCustodioAsync();

            entrada.FondoCajaChicaId = fondos.FirstOrDefault()?.Id ?? Guid.Empty;
            entrada.CategoriaGastoId = categorias.FirstOrDefault()?.Id ?? Guid.Empty;
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

        private void SeleccionarArchivos(InputFileChangeEventArgs e)
        {
            errores.Clear();
            adjuntos.Clear();

            foreach (var archivo in e.GetMultipleFiles(maximumFileCount: 20))
            {
                adjuntos.Add(new AdjuntoSeleccionado(archivo));
            }
        }

        private void QuitarAdjunto(AdjuntoSeleccionado adjunto)
        {
            adjuntos.Remove(adjunto);
            errores.Clear();
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
            guardando = true;

            // Los streams de IBrowserFile se abren aquí, dentro del envío, y se cierran al
            // terminar: no se pueden guardar abiertos entre interacciones.
            var abiertos = new List<Stream>();

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
                    MontoTotal = entrada.MontoTotal ?? 0,
                    FechaGasto = entrada.FechaGasto
                };

                foreach (var adjunto in adjuntos)
                {
                    var stream = adjunto.Archivo.OpenReadStream(TamanoMaximoArchivo);
                    abiertos.Add(stream);

                    comando.Comprobantes.Add(new RegistrarGastoCommand.ComprobanteEntrada
                    {
                        NombreOriginal = adjunto.Archivo.Name,
                        TipoMime = adjunto.Archivo.ContentType,
                        TamanoBytes = adjunto.Archivo.Size,
                        Descripcion = adjunto.Descripcion,
                        Contenido = stream
                    });
                }

                var resultado = await Handler.EjecutarAsync(comando);

                if (!resultado.Exitoso)
                {
                    errores.AddRange(resultado.Errores);
                    return;
                }

                exito = "Gasto registrado correctamente.";
                entrada.Limpiar(fondos!.FirstOrDefault()?.Id ?? Guid.Empty, categorias!.FirstOrDefault()?.Id ?? Guid.Empty);
                adjuntos.Clear();

                // El balance del fondo cambió, hay que releerlo para el próximo registro.
                fondos = await Fondos.ListarAsync();
            }
            catch (Exception ex)
            {
                errores.Add(ex.Message);
            }
            finally
            {
                foreach (var stream in abiertos)
                {
                    await stream.DisposeAsync();
                }

                guardando = false;
            }
        }

        private sealed class AdjuntoSeleccionado
        {
            public AdjuntoSeleccionado(IBrowserFile archivo) => Archivo = archivo;

            public IBrowserFile Archivo { get; }
            public string Descripcion { get; set; } = string.Empty;
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
            public decimal? Subtotal { get; set; }
            public decimal? MontoITBIS { get; set; }
            public decimal? MontoTotal { get; set; }
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
                MontoTotal = null;
                FechaGasto = DateTime.Today;
            }
        }
    }
}
