using System;
using Microsoft.AspNetCore.Identity;

namespace EDEEste.ControlCajaChica.Infrastructure.Identity
{
    /// <summary>
    /// Entidad de identidad de la aplicacion.
    ///
    /// Vive en Infrastructure y no en Domain porque hereda de IdentityUser, es decir
    /// es un detalle del proveedor de autenticacion que elegimos. Domain se mantiene
    /// libre de frameworks y las demas entidades se refieren al usuario solo por su
    /// Id (string), no por esta clase.
    ///
    /// El rol NO se guarda aqui: la fuente de verdad son las tablas AspNetRoles /
    /// AspNetUserRoles que administra RoleManager/UserManager. Tener ademas una
    /// columna "Rol" creaba dos verdades que se desincronizaban.
    /// </summary>
    public class Usuario : IdentityUser
    {
        public string Nombre { get; set; } = string.Empty;

        /// <summary>
        /// Baja logica del usuario. Es independiente de LockoutEnd: Activo lo maneja
        /// el Administrador, el lockout lo maneja Identity por intentos fallidos.
        /// </summary>
        public bool Activo { get; set; } = true;
    }
}
