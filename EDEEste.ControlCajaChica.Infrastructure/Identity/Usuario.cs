using System;
using EDEEste.ControlCajaChica.Domain.Enums;
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
        /// Si esta cuenta puede entrar al sistema. Nace <see cref="EstadoAccesoUsuario.Pendiente"/>:
        /// registrarse no da acceso, un Administrador tiene que revisar quien es y
        /// asignarle un rol. Es independiente de LockoutEnd, que lo maneja Identity
        /// por intentos fallidos de contrasena.
        /// </summary>
        public EstadoAccesoUsuario EstadoAcceso { get; set; } = EstadoAccesoUsuario.Pendiente;

        /// <summary>
        /// Cuando se creo la cuenta -- para una cuenta todavia Pendiente es, en la
        /// practica, "cuando se solicito el acceso". Se inicializa aqui (no en el
        /// momento de guardar) para que quede fijada al instante de construir el
        /// objeto, igual para una cuenta creada por CrearUsuarioAsync que por
        /// CrearUsuarioPendienteAsync.
        /// </summary>
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Cuando inicio sesion la ultima vez. Null hasta el primer inicio de sesion
        /// exitoso. Login.razor.cs lee este valor ANTES de sobreescribirlo (es la
        /// sesion anterior a la que esta entrando) y lo pasa como claim de la propia
        /// sesion -- ver <see cref="EDEEste.ControlCajaChica.Domain.Constants.ClaimsApp.UltimoAccesoAnterior"/>.
        /// </summary>
        public DateTime? UltimoAccesoUtc { get; set; }

        /// <summary>
        /// Ruta relativa de la foto de perfil dentro del almacenamiento
        /// (uploads/perfil/...), o null si no tiene y se muestran las iniciales.
        ///
        /// Guarda la ruta y no los bytes a proposito: una imagen en la fila del usuario
        /// se arrastraria en cada consulta que materialice la entidad (Identity la
        /// carga entera en cada login y en cada UserManager.GetUserAsync).
        ///
        /// No entra en ninguna firma HMAC: esta clase vive fuera de la jerarquia
        /// AuditableEntity/ITamperProofEntity del Domain -- una foto de perfil no es
        /// evidencia contable, a diferencia de un comprobante.
        /// </summary>
        public string? RutaFotoPerfil { get; set; }
    }
}
