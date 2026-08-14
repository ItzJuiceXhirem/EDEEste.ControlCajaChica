using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using EDEEste.ControlCajaChica.Domain.Common;
using EDEEste.ControlCajaChica.Domain.Interfaces;

namespace EDEEste.ControlCajaChica.Domain.Entities
{
    public class ComprobanteAdjunto : AuditableEntity, ITamperProofEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid GastoId { get; set; }
        public Gasto? Gasto { get; set; }

        public string NombreOriginal { get; set; } = string.Empty;
        public string RutaArchivo { get; set; } = string.Empty;
        public string TipoMime { get; set; } = string.Empty; // Ej: application/pdf, image/jpeg
        public long TamanoBytes { get; set; }
        public string HashSHA256 { get; set; } = string.Empty; //Para validar que el PDF no fue alterado
        public DateTime FechaSubida { get; set; } = DateTime.UtcNow;

        // se implementa la interfaz para el sello HMAC de la fila en la BDD
        public string HashFirma { get ; set ; } = string.Empty;

        [NotMapped]
        public bool IntegridadVerificada { get; set; } = true;

        // El HashSHA256 protege el archivo fisico; esta firma protege la fila que
        // apunta a el, para que nadie pueda repuntar el comprobante a otro gasto
        // ni a otro archivo.
        public string ObtenerCadenaParaHash() =>
            new ConstructorFirma(nameof(ComprobanteAdjunto))
                .Agregar(Id)
                .Agregar(GastoId)
                .Agregar(NombreOriginal)
                .Agregar(RutaArchivo)
                .Agregar(TipoMime)
                .Agregar(TamanoBytes)
                .Agregar(HashSHA256)
                .Agregar(FechaSubida)
                .Agregar(IsDeleted)
                .ToString();
    }
}
