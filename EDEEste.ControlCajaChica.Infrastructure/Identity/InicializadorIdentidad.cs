using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Constants;
using EDEEste.ControlCajaChica.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EDEEste.ControlCajaChica.Infrastructure.Identity
{
    /// <summary>
    /// Siembra en la BDD los roles declarados en <see cref="RolesApp"/> al arrancar.
    /// Es idempotente, asi que puede correr en cada arranque sin duplicar nada.
    /// </summary>
    public sealed class InicializadorIdentidad
    {
        private readonly IIdentityService _identityService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<InicializadorIdentidad> _logger;

        public InicializadorIdentidad(
            IIdentityService identityService,
            ApplicationDbContext context,
            ILogger<InicializadorIdentidad> logger)
        {
            _identityService = identityService;
            _context = context;
            _logger = logger;
        }

        public async Task SembrarRolesAsync(CancellationToken cancellationToken = default)
        {
            // Si la BDD todavia no existe (proyecto recien clonado, migraciones sin
            // aplicar) no tiene sentido reventar el arranque: se avisa y se sigue.
            if (!await _context.Database.CanConnectAsync(cancellationToken))
            {
                _logger.LogWarning(
                    "No se pudo conectar a la base de datos, se omitio la siembra de roles. " +
                    "Ejecute 'dotnet ef database update' y vuelva a iniciar la aplicacion.");
                return;
            }

            foreach (var rol in RolesApp.Todos)
            {
                await _identityService.AsegurarRolAsync(rol);
            }

            _logger.LogInformation(
                "Roles verificados en la base de datos: {Roles}.",
                string.Join(", ", RolesApp.Todos));
        }
    }
}
