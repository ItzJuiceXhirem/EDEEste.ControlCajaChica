using System;
using System.Collections.Generic;

namespace EDEEste.ControlCajaChica.Application.Features.Gastos
{
    // Datos para registrar un gasto de caja chica junto con sus comprobantes
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
        /// Un comprobante ya subido a staging (ver GastoEndpoints, endpoint POST
        /// /gastos/comprobantes/staging) y la transcripción que le puso el usuario.
        ///
        /// No lleva nombre, MIME, tamaño ni contenido: esos datos nunca se confía en
        /// ellos si vienen del cliente -- el handler los resuelve del lado del
        /// servidor a partir de Referencia (via IFileStorageService), leyéndolos del
        /// manifiesto firmado que se guardó al subir el archivo.
        /// </summary>
        public sealed class ComprobanteEntrada
        {
            public Guid Referencia { get; set; }
            public string Descripcion { get; set; } = string.Empty;
        }
    }
}
