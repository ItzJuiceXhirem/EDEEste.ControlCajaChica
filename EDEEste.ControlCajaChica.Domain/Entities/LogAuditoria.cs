using System;
using System.Collections.Generic;
using System.Text;
using EDEEste.ControlCajaChica.Domain.Common;

namespace EDEEste.ControlCajaChica.Domain.Entities
{
    /// Bitacora criptografica. Cada fila se firma con HMAC incluyendo la firma de la
    /// fila anterior, formando una cadena: firmar una fila sola detecta ediciones,
    /// pero encadenarlas es lo que detecta <b>borrados</b>. Si alguien elimina o
    /// reordena un log desde la BDD, el HashAnterior de la fila siguiente deja de
    /// coincidir y la cadena se rompe de forma visible.
    public class LogAuditoria
    {
        // Valor de <see cref="HashAnterior"/> para el primer log de la cadena
        public const string HashGenesis = "GENESIS";

        public Guid Id { get; set; } = Guid.NewGuid();

        /// Correlativo generado por la BDD. No entra en la firma (no se conoce antes
        /// del INSERT); solo define el orden con el que se recorre la cadena.
        public long Secuencia { get; set; }

        public string UsuarioId { get; set; } = string.Empty;
        public string TipoAccion { get; set; } = string.Empty;
        public string NombreTabla { get; set; } = string.Empty;
        public string RegistroId { get; set; } = string.Empty;
        public string? ValoresAnteriores { get; set; }
        public string? ValoresNuevos { get; set; }
        public DateTime FechaEjecucion { get; set; } = DateTime.UtcNow;

        // Firma del log inmediatamente anterior, o <see cref="HashGenesis"/>.
        public string HashAnterior { get; set; } = HashGenesis;

        // HMAC de esta fila, calculado sobre <see cref="ObtenerCadenaParaHash"/>.
        public string HashFirma { get; set; } = string.Empty;

        public string ObtenerCadenaParaHash() =>
            new ConstructorFirma(nameof(LogAuditoria))
                .Agregar(Id)
                .Agregar(UsuarioId)
                .Agregar(TipoAccion)
                .Agregar(NombreTabla)
                .Agregar(RegistroId)
                .Agregar(ValoresAnteriores)
                .Agregar(ValoresNuevos)
                .Agregar(FechaEjecucion)
                .Agregar(HashAnterior)
                .ToString();
    }
}
