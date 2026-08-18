using System;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Infrastructure.Configuration;
using EDEEste.ControlCajaChica.Infrastructure.Identity;
using EDEEste.ControlCajaChica.Infrastructure.Persistence;
using EDEEste.ControlCajaChica.Infrastructure.Persistence.Interceptors;
using EDEEste.ControlCajaChica.Infrastructure.Repositories;
using EDEEste.ControlCajaChica.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EDEEste.ControlCajaChica.Infrastructure
{
    /// <summary>
    /// Registro unico de la capa de infraestructura. Program.cs solo llama
    /// AddInfrastructure y no necesita conocer las implementaciones concretas.
    /// </summary>
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            AgregarCriptografia(services, configuration);
            AgregarPersistencia(services, configuration);

            services.AddScoped<IIdentityService, IdentityService>();
            services.AddScoped<IReporteGastosService, QuestPdfReporteService>();
            services.AddScoped<IFileStorageService, FileStorageService>();
            services.AddScoped<IPdfConsolidadorService, PdfConsolidadorService>();
            services.AddScoped<InicializadorIdentidad>();

            AgregarRepositorios(services);

            return services;
        }

        /// <summary>
        /// Los repositorios comparten el mismo ApplicationDbContext con scope que usan
        /// los casos de uso, asi que lo que marcan queda pendiente en el mismo
        /// ChangeTracker y se confirma con un unico SaveChangesAsync.
        /// </summary>
        private static void AgregarRepositorios(IServiceCollection services)
        {
            services.AddScoped<IFondoRepository, FondoRepository>();
            services.AddScoped<ICategoriaGastoRepository, CategoriaGastoRepository>();
            services.AddScoped<IGastoRepository, GastoRepository>();
            services.AddScoped<IReposicionRepository, ReposicionRepository>();
        }

        private static void AgregarCriptografia(IServiceCollection services, IConfiguration configuration)
        {
            var claveHmac = configuration.GetSection(OpcionesCriptografia.Seccion)["ClaveHmac"];

            // Se falla en el arranque, no en el primer guardado: una app corriendo sin
            // clave firmaria con basura o reventaria a mitad de una operacion contable.
            if (string.IsNullOrWhiteSpace(claveHmac))
            {
                throw new InvalidOperationException(
                    $"Falta la clave HMAC '{OpcionesCriptografia.Seccion}:ClaveHmac'. " +
                    "Generela y guardela fuera del control de versiones, por ejemplo:\r\n" +
                    "  dotnet user-secrets set \"Criptografia:ClaveHmac\" \"<clave-base64-de-32-bytes>\"");
            }

            byte[] claveBytes;
            try
            {
                claveBytes = Convert.FromBase64String(claveHmac);
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException(
                    $"La clave '{OpcionesCriptografia.Seccion}:ClaveHmac' no es Base64 valido.", ex);
            }

            if (claveBytes.Length < OpcionesCriptografia.BytesMinimosClave)
            {
                throw new InvalidOperationException(
                    $"La clave '{OpcionesCriptografia.Seccion}:ClaveHmac' debe tener al menos " +
                    $"{OpcionesCriptografia.BytesMinimosClave} bytes ({OpcionesCriptografia.BytesMinimosClave * 8} bits); " +
                    $"la configurada tiene {claveBytes.Length}.");
            }

            services.Configure<OpcionesCriptografia>(opciones => opciones.ClaveHmac = claveHmac);
            services.AddScoped<ICriptografiaService, CriptografiaService>();
        }

        private static void AgregarPersistencia(IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetSection("ConnectionStrings")["DefaultConnection"]
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            // Los interceptores se registran como IInterceptor y NO se pasan por
            // options.AddInterceptors(...).
            //
            // Pasarlos explicitamente obligaba a resolverlos dentro del lambda de
            // AddDbContext, lo que entregaba instancias distintas en cada scope. EF
            // usa las opciones como clave de cache de su proveedor de servicios
            // interno, asi que cada peticion le parecia una configuracion nueva y
            // construia otro proveedor; pasadas 20 peticiones reventaba con
            // ManyServiceProvidersCreatedWarning.
            //
            // Registrandolos asi, EF los descubre desde el contenedor de la
            // aplicacion: las opciones quedan identicas entre peticiones (un solo
            // proveedor interno) y los interceptores siguen siendo Scoped, que es lo
            // que necesitan para ver el usuario actual de esa peticion.
            services.AddScoped<IInterceptor, AuditoriaInterceptor>();
            services.AddScoped<IInterceptor, IntegridadInterceptor>();

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));

            services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        }
    }
}
