using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Domain.Enums;
using EDEEste.ControlCajaChica.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace EDEEste.ControlCajaChica.Presentation.Services
{
    /// <summary>
    /// Decide si una cuenta puede iniciar sesion: solo las aprobadas por un
    /// Administrador.
    ///
    /// Se engancha en <see cref="IUserConfirmation{TUser}"/>, que es el punto que
    /// consulta SignInManager.CanSignInAsync cuando SignIn.RequireConfirmedAccount
    /// esta activo. El nombre de esa opcion viene de la confirmacion por correo del
    /// scaffold, pero el gancho es generico ("¿esta cuenta esta habilitada?") y aqui
    /// se reinterpreta como "¿un Administrador ya aprobo este acceso?".
    ///
    /// Se prefirio esto a heredar de SignInManager porque el constructor de esa clase
    /// cambia entre versiones de .NET y heredarla obliga a reproducir su firma
    /// completa; este gancho es estable y es el que el propio framework expone.
    ///
    /// Es la segunda capa de defensa: Login.razor ya comprueba el estado antes de
    /// emitir la cookie, pero esto cubre cualquier otra ruta de inicio de sesion que
    /// pase por PasswordSignInAsync.
    /// </summary>
    public sealed class ConfirmacionAccesoUsuario : IUserConfirmation<Usuario>
    {
        public Task<bool> IsConfirmedAsync(UserManager<Usuario> manager, Usuario user) =>
            Task.FromResult(user.EstadoAcceso == EstadoAccesoUsuario.Aprobado);
    }
}
