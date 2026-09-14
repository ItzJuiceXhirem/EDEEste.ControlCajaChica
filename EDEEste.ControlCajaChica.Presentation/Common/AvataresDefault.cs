using System;

namespace EDEEste.ControlCajaChica.Presentation.Common
{
    /// <summary>
    /// Texto del avatar por defecto (iniciales) para cuando un usuario no tiene
    /// foto de perfil propia -- compartido por Usuarios y ManageLayout, que lo
    /// tenian copiado identico cada una.
    /// </summary>
    public static class AvataresDefault
    {
        /// <summary>
        /// Primera letra del primer y del ultimo nombre/apellido, para que
        /// "Gerente Principal" de "GP" en vez de "GE".
        /// </summary>
        public static string Iniciales(string nombreCompleto)
        {
            var partes = nombreCompleto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return partes.Length switch
            {
                0 => "?",
                1 => partes[0][..1].ToUpperInvariant(),
                _ => (partes[0][..1] + partes[^1][..1]).ToUpperInvariant()
            };
        }
    }
}
