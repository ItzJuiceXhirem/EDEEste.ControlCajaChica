using System.Threading;

namespace EDEEste.ControlCajaChica.Infrastructure.Persistence.Interceptors
{
    /// <summary>
    /// Guarda el id del usuario actual en un AsyncLocal para que AuditoriaInterceptor
    /// (Singleton, para que EF no reconstruya su proveedor de servicios interno en
    /// cada peticion) pueda saber quien esta autenticado sin depender de
    /// ICurrentUserService (Scoped) por constructor.
    ///
    /// Funciona porque ICurrentUserService.ObtenerAsync() es el unico punto por el que
    /// pasan todos los handlers de la aplicacion justo antes de guardar (es el patron
    /// establecido en RegistrarGastoHandler, AprobarReposicionHandler, etc.), y
    /// AsyncLocal viaja correctamente por la misma cadena de async/await: el valor que
    /// se fija ahi sigue visible cuando, mas adelante en el MISMO flujo logico, corre
    /// AuditoriaInterceptor.SavingChanges/SavingChangesAsync.
    ///
    /// Se probaron dos alternativas antes de esta y las dos revientan con
    /// ManyServiceProvidersCreatedWarning pasadas ~20 peticiones: pasar los
    /// interceptores Scoped por el lambda de AddDbContext, y pasarlos Scoped via
    /// constructor de ApplicationDbContext + OnConfiguring. En ambos casos EF ve una
    /// instancia de interceptor distinta en cada peticion y reconstruye su proveedor
    /// interno. La unica forma de que los interceptores sean realmente Singleton (una
    /// sola instancia, estable, para que EF no reconstruya nada) y aun asi sepan quien
    /// es el usuario de CADA peticion es no pedirselo a un contenedor de DI en
    /// absoluto: leerlo de un valor ambiente que viaja solo con el flujo de ejecucion.
    /// </summary>
    public static class AmbientUsuarioActual
    {
        private static readonly AsyncLocal<string?> _usuarioId = new();

        public static string? UsuarioId
        {
            get => _usuarioId.Value;
            set => _usuarioId.Value = value;
        }
    }
}
