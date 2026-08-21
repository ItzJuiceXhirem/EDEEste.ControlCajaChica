using System;
using System.Collections.Generic;
using System.IO;

namespace EDEEste.ControlCajaChica.Application.Features.Gastos
{
    /// <summary>
    /// Datos para registrar un gasto de caja chica junto con sus comprobantes.
    /// </summary>
    public sealed class RegistrarGastoCommand
    {
        public Guid FondoCajaChicaId { get; set; }
        public Guid CategoriaGastoId { get; set; }

        public string Proveedor { get; set; } = string.Empty;
        public string RNCProveedor { get; set; } = string.Empty;
        public string NCF { get; set; } = string.Empty;
        public string? Concepto { get; set; }

        public decimal Subtotal { get; set; }
        public decimal MontoITBIS { get; set; }
        public decimal MontoTotal { get; set; }
        public DateTime FechaGasto { get; set; } = DateTime.Today;

        public List<ComprobanteEntrada> Comprobantes { get; set; } = new();

        /// <summary>
        /// Un archivo adjunto y su transcripcion. El contenido viaja como Stream para
        /// no cargar en memoria facturas grandes: FileStorageService lo copia a disco
        /// por bloques.
        /// </summary>
        public sealed class ComprobanteEntrada
        {
            public string NombreOriginal { get; set; } = string.Empty;
            public string TipoMime { get; set; } = string.Empty;
            public long TamanoBytes { get; set; }
            public string Descripcion { get; set; } = string.Empty;
            public Stream Contenido { get; set; } = Stream.Null;
        }
    }
}
