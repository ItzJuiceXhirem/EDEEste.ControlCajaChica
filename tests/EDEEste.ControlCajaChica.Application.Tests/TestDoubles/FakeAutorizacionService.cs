using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;

namespace EDEEste.ControlCajaChica.Application.Tests.TestDoubles
{
    /// <summary>Concede todos los permisos por defecto: las pruebas de negocio no
    /// necesitan simular un usuario sin permiso salvo que lo pidan explícitamente.</summary>
    public sealed class FakeAutorizacionService : IAutorizacionService
    {
        private readonly bool _tienePermiso;

        public FakeAutorizacionService(bool tienePermiso = true) => _tienePermiso = tienePermiso;

        public Task<bool> TienePermisoAsync(string permiso, CancellationToken cancellationToken = default) =>
            Task.FromResult(_tienePermiso);
    }
}
