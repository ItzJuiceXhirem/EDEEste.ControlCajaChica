using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Common.Models;

namespace EDEEste.ControlCajaChica.Application.Tests.TestDoubles
{
    public sealed class FakeCurrentUserService : ICurrentUserService
    {
        private readonly UsuarioActual _usuario;

        public FakeCurrentUserService(string id = "usuario-prueba", string nombre = "Usuario Prueba") =>
            _usuario = new UsuarioActual(id, nombre, true);

        public Task<UsuarioActual> ObtenerAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_usuario);
    }
}
