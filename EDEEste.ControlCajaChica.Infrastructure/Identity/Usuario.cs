using System;
using EDEEste.ControlCajaChica.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace EDEEste.ControlCajaChica.Infrastructure.Identity
{
    /// <summary>
    /// Entidad de identidad de la aplicación.
    ///
    /// Vive en Infrastructure y no en Domain porque hereda de IdentityUser, es decir
    /// es un detalle del proveedor de autenticación que elegimos. Domain se mantiene
    /// libre de frameworks y las demás entidades se refieren al usuario solo por su
    /// Id (string), no por esta clase.
    ///
    /// El rol NO se guarda aquí: la fuente de verdad son las tablas AspNetRoles /
    /// AspNetUserRoles que administra RoleManager/UserManager. Tener además una
    /// columna "Rol" creaba dos verdades que se desincronizaban.
    /// </summary>
    public class Usuario : IdentityUser
    {
        public string Nombre { get; set; } = string.Empty;

        /// <summary>
        /// Si esta cuenta puede entrar al sistema. Nace <see cref="EstadoAccesoUsuario.Pendiente"/>:
        /// registrarse no da acceso, un Administrador tiene que revisar quién es y
        /// asignarle un rol. Es independiente de LockoutEnd, que lo maneja Identity
        /// por intentos fallidos de contraseña.
        /// </summary>
        public EstadoAccesoUsuario EstadoAcceso { get; set; } = EstadoAccesoUsuario.Pendiente;

        /// <summary>
        /// Cuándo se creó la cuenta -- para una cuenta todavía Pendiente es, en la
        /// práctica, "cuándo se solicitó el acceso". Se inicializa aquí (no en el
        /// momento de guardar) para que quede fijada al instante de construir el
        /// objeto.
        /// </summary>
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Cuándo inicio sesión la última vez. Null hasta el primer inicio de sesión
        /// exitoso. Login.razor.cs lee este valor ANTES de sobreescribirlo (es la
        /// sesión anterior a la que está entrando) y lo pasa como claim de la propia
        /// sesión -- ver <see cref="EDEEste.ControlCajaChica.Domain.Constants.ClaimsApp.UltimoAccesoAnterior"/>.
        /// </summary>
        public DateTime? UltimoAccesoUtc { get; set; }

        /// <summary>
        /// Ruta relativa de la foto de perfil dentro del almacenamiento
        /// (uploads/perfil/...), o null si no tiene y se muestran las iniciales.
        ///
        /// Guarda la ruta y no los bytes a propósito: una imagen en la fila del usuario
        /// se arrastraría en cada consulta que materialice la entidad (Identity la
        /// carga entera en cada login y en cada UserManager.GetUserAsync).
        /// </summary>
        public string? RutaFotoPerfil { get; set; }

        // "oscuro" o null (claro) -- el default es siempre claro.
        public string? TemaPreferido { get; set; }
    }
}
