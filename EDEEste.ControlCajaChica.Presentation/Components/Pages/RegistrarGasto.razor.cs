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
        // Los adjuntos de una factura son fotos o PDF de pocas paginas; 10 MB deja
        // holgura de sobra y evita que el limite de 512 KB que trae InputFile por
        // defecto corte una foto de celular.
        private const long TamanoMaximoArchivo = 10 * 1024 * 1024;

        [Inject]
        private IFondoRepository Fondos { get; set; } = default!;

        [Inject]
        private ICategoriaGastoRepository Categorias { get; set; } = default!;

        [Inject]
        private RegistrarGastoHandler Handler { get; set; } = default!;

        private IReadOnlyList<FondoCajaChica>? fondos;
        private IReadOnlyList<CategoriaGasto>? categorias;

        private readonly EntradaGasto entrada = new();
        private readonly List<AdjuntoSeleccionado> adjuntos = new();
        private readonly List<string> errores = new();

        private bool guardando;
        private string? exito;

        protected override async Task OnInitializedAsync()
        {
            fondos = await Fondos.ListarAsync();
            categorias = await Categorias.ListarActivasAsync();

            entrada.FondoCajaChicaId = fondos.FirstOrDefault()?.Id ?? Guid.Empty;
            entrada.CategoriaGastoId = categorias.FirstOrDefault()?.Id ?? Guid.Empty;
        }

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
        /// Deja solo digitos y, cuando llegan a 11 (una cedula), los muestra como
        /// XXX-XXXXXXX-X. Un RNC de empresa (9 digitos) se deja sin guiones. Lo que se
        /// envia al handler se vuelve a limpiar alli, asi que en BDD solo entran digitos.
        /// </summary>
        private void FormatearRnc(ChangeEventArgs e)
        {
            var digitos = new string((e.Value?.ToString() ?? string.Empty).Where(char.IsDigit).ToArray());

            if (digitos.Length > 11)
            {
                digitos = digitos[..11];
            }

            entrada.RNCProveedor = digitos.Length == 11
                ? $"{digitos[..3]}-{digitos[3..10]}-{digitos[10]}"
                : digitos;
        }

        private async Task GuardarAsync()
        {
            errores.Clear();
            exito = null;
            guardando = true;

            // Los streams de IBrowserFile se abren aqui, dentro del envio, y se cierran al
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

                // El balance del fondo cambio, hay que releerlo para el proximo registro.
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

            // Nullables para que el campo salga vacio y se vea el placeholder "0" en vez
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
