using System;

namespace EDEEste.ControlCajaChica.Application.DTOs
{
    /// <summary>
    /// Un registro cuya firma HMAC no coincide con sus datos: alguien lo modificó
    /// directamente en la base de datos, sin pasar por la aplicación.
    /// </summary>
    public sealed class RegistroConFirmaInvalidaDto
    {
        // Nombre de la tabla, el mismo que usa la bitácora (LogAuditoria.NombreTabla).
        public string NombreTabla { get; init; } = string.Empty;
        public Guid Id { get; init; }

        // Unos pocos datos para reconocer el registro a simple vista.
        public string Descripcion { get; init; } = string.Empty;

        public DateTime FechaCreacion { get; init; }
        public DateTime? FechaModificacion { get; init; }
        public bool Eliminado { get; init; }
    }
}
