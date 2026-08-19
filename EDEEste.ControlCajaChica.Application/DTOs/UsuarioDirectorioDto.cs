using System;

namespace EDEEste.ControlCajaChica.Application.DTOs
{
    /// <summary>
    /// Ficha de un usuario en el Active Directory de la empresa, tal como la devuelve
    /// el APICommon en /api/ActiveDirectory/GetUserByUserName.
    ///
    /// Los nombres calcan el contrato del API (que viene en camelCase); la
    /// deserializacion se configura como insensible a mayusculas en Infrastructure,
    /// para no ensuciar este DTO con atributos de serializacion.
    ///
    /// Todo es nullable salvo el usuario: no controlamos ese API y una ficha
    /// incompleta (sin telefono, sin supervisor) es perfectamente normal.
    /// </summary>
    public sealed record UsuarioDirectorioDto
    {
        public int Codigo { get; init; }
        public string Usuario { get; init; } = string.Empty;
        public string? DisplayName { get; init; }
        public string? Nombre { get; init; }
        public string? Apellido { get; init; }
        public string? Posicion { get; init; }
        public string? Departamento { get; init; }
        public string? Correo { get; init; }
        public string? Cedula { get; init; }
        public string? LugarDeTrabajo { get; init; }
        public string? Telefono { get; init; }
        public string? Supervisor { get; init; }

        /// <summary>
        /// Nombre para mostrar, cayendo a "Nombre Apellido" y por ultimo al usuario.
        /// El APICommon no garantiza que displayName venga siempre.
        /// </summary>
        public string NombreParaMostrar =>
            !string.IsNullOrWhiteSpace(DisplayName) ? DisplayName!
            : !string.IsNullOrWhiteSpace($"{Nombre} {Apellido}".Trim()) ? $"{Nombre} {Apellido}".Trim()
            : Usuario;
    }
}
