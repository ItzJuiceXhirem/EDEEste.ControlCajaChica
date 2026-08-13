using System;
using System.Collections.Generic;
using System.Text;
using EDEEste.ControlCajaChica.Domain.Interfaces;

namespace EDEEste.ControlCajaChica.Domain.Entities
{
    public class ComprobanteAdjunto : AuditableEntity, ITamperProofEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Gasto GastoId { get; set; } //FK
        public string NombreOriginal { get; set; } = string.Empty;
        public string RutaArchivo { get; set; } = string.Empty;
        public string TipoMime { get; set; } = string.Empty; // Ej: application/pdf, image/jpeg
        public long TamanoBytes { get; set; }
        public string HashSHA256 { get; set; } = string.Empty; //Para validar que el PDF no fue alterado
        public DateTime FechaSubida { get; set; } = DateTime.UtcNow;
        
        // se implementa la interfaz para el sello HMAC de la fila en la BDD
        public string HashFirma { get ; set ; } = string.Empty;

        public string ObtenerCadenaParaHash()
        {
            return $"{Id} | {GastoId} | {HashSHA256} | {RutaArchivo}";
        }
    }
}
