using System;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Infrastructure.Configuration;
using EDEEste.ControlCajaChica.Infrastructure.Identity;
using EDEEste.ControlCajaChica.Infrastructure.Persistence;
using EDEEste.ControlCajaChica.Infrastructure.Persistence.Interceptors;
using EDEEste.ControlCajaChica.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
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

            return services;
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

            services.AddScoped<AuditoriaInterceptor>();
            services.AddScoped<IntegridadInterceptor>();

            services.AddDbContext<ApplicationDbContext>((sp, options) =>
                options.UseSqlServer(connectionString)
                       .AddInterceptors(
                           sp.GetRequiredService<AuditoriaInterceptor>(),
                           sp.GetRequiredService<IntegridadInterceptor>()));

            services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        }
    }
}
