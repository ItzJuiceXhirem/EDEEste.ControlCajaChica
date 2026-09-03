using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Application.DTOs;
using EDEEste.ControlCajaChica.Domain.Enums;

namespace EDEEste.ControlCajaChica.Application.Tests.TestDoubles
{
    /// <summary>Por defecto nadie esta en ningun rol: las pruebas de negocio no
    /// necesitan simular Custodio salvo que lo pidan explicitamente, y asi la
    /// comprobacion de pertenencia de fondo queda inactiva igual que antes de que
    /// existiera. Solo implementa EstaEnRolAsync, que es lo unico que llaman los
    /// handlers; el resto no lo necesita ningun test de handler.</summary>
    public sealed class FakeIdentityService : IIdentityService
    {
        private readonly string _usuarioId;
        private readonly HashSet<string> _roles;

        public FakeIdentityService(string usuarioId = "usuario-prueba", params string[] roles)
        {
            _usuarioId = usuarioId;
            _roles = new HashSet<string>(roles);
        }

        public Task<bool> EstaEnRolAsync(string usuarioId, string rol) =>
            Task.FromResult(usuarioId == _usuarioId && _roles.Contains(rol));

        public Task<string?> ObtenerNombreUsuarioAsync(string usuarioId) => throw new NotImplementedException();

        public Task<IReadOnlyDictionary<string, string>> ObtenerNombresUsuarioAsync(IEnumerable<string> usuarioIds) =>
            throw new NotImplementedException();

        public Task<ResultadoIdentidad> CrearUsuarioAsync(string usuario, string password, string nombre, string rol) =>
            throw new NotImplementedException();

        public Task<ResultadoIdentidad> CrearUsuarioPendienteAsync(string usuario, string password, string nombre) =>
            throw new NotImplementedException();

        public Task AsegurarRolAsync(string rol) => throw new NotImplementedException();

        public Task<EstadoAccesoUsuario?> ObtenerEstadoAccesoAsync(string usuarioId) => throw new NotImplementedException();

        public Task<ResultadoIdentidad> AprobarAccesoAsync(string usuarioId, string rol) => throw new NotImplementedException();

        public Task<ResultadoIdentidad> DenegarAccesoAsync(string usuarioId) => throw new NotImplementedException();

        public Task<ResultadoIdentidad> CambiarRolAsync(string usuarioId, string nuevoRol) => throw new NotImplementedException();

        public Task<IReadOnlyList<UsuarioResumenDto>> ListarUsuariosAsync(EstadoAccesoUsuario? estado = null) =>
            throw new NotImplementedException();

        public Task<bool> ExisteAdministradorAsync() => throw new NotImplementedException();

        public Task<DateTime?> RegistrarAccesoAsync(string usuarioId) => throw new NotImplementedException();
    }
}
