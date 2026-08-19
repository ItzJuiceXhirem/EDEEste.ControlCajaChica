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
using Microsoft.Extensions.Options;

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
            AgregarAutenticacion(services, configuration);

            services.AddScoped<IIdentityService, IdentityService>();
            services.AddScoped<IPasswordResetService, PasswordResetService>();
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

        /// <summary>
        /// Elige de donde salen las contrasenas segun <c>Autenticacion:Modo</c>.
        ///
        /// El cliente del APICommon se registra SIEMPRE, incluso en modo Local:
        /// consultar la ficha de alguien en el directorio es util por si solo y no
        /// depende de que las contrasenas se validen alli.
        /// </summary>
        private static void AgregarAutenticacion(IServiceCollection services, IConfiguration configuration)
        {
            var seccion = configuration.GetSection(OpcionesAutenticacion.Seccion);
            var modoTexto = seccion["Modo"];

            var modo = ModoAutenticacion.Local;
            if (!string.IsNullOrWhiteSpace(modoTexto) && !Enum.TryParse(modoTexto, ignoreCase: true, out modo))
            {
                throw new InvalidOperationException(
                    $"El valor '{modoTexto}' de '{OpcionesAutenticacion.Seccion}:Modo' no es valido. " +
                    $"Use '{nameof(ModoAutenticacion.Local)}' o '{nameof(ModoAutenticacion.ActiveDirectory)}'.");
            }

            services.Configure<OpcionesAutenticacion>(opciones => opciones.Modo = modo);

            var apiCommon = configuration.GetSection(OpcionesApiCommon.Seccion);
            var urlBase = apiCommon["UrlBase"];
            var apiKey = apiCommon["ApiKey"];

            // Solo se exige la configuracion del APICommon si de verdad se va a
            // depender de el para entrar. En modo Local, que falte la API Key es lo
            // normal (todavia no la tenemos) y no debe impedir arrancar.
            if (modo == ModoAutenticacion.ActiveDirectory)
            {
                if (string.IsNullOrWhiteSpace(urlBase))
                {
                    throw new InvalidOperationException(
                        $"Falta '{OpcionesApiCommon.Seccion}:UrlBase' y '{OpcionesAutenticacion.Seccion}:Modo' " +
                        $"esta en {nameof(ModoAutenticacion.ActiveDirectory)}. Configurela, por ejemplo en appsettings.json:\r\n" +
                        "  \"ApiCommon\": { \"UrlBase\": \"http://inapprueba/apicommon\" }");
                }

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    throw new InvalidOperationException(
                        $"Falta la clave '{OpcionesApiCommon.Seccion}:ApiKey' y '{OpcionesAutenticacion.Seccion}:Modo' " +
                        $"esta en {nameof(ModoAutenticacion.ActiveDirectory)}. Guardela fuera del control de versiones:\r\n" +
                        "  dotnet user-secrets set \"ApiCommon:ApiKey\" \"<clave>\" --project EDEEste.ControlCajaChica.Presentation\r\n" +
                        $"Mientras no la tenga, deje '{OpcionesAutenticacion.Seccion}:Modo' en {nameof(ModoAutenticacion.Local)}.");
                }
            }

            services.Configure<OpcionesApiCommon>(opciones =>
            {
                opciones.UrlBase = urlBase ?? string.Empty;
                opciones.ApiKey = apiKey ?? string.Empty;

                var cabecera = apiCommon["NombreCabeceraApiKey"];
                if (!string.IsNullOrWhiteSpace(cabecera))
                {
                    opciones.NombreCabeceraApiKey = cabecera;
                }
            });

            ConfigurarClienteApiCommon(services.AddHttpClient<IDirectorioActivoService, DirectorioActivoApiCommon>());

            if (modo == ModoAutenticacion.ActiveDirectory)
            {
                ConfigurarClienteApiCommon(
                    services.AddHttpClient<IAutenticadorCredenciales, AutenticacionActiveDirectory>());
            }
            else
            {
                services.AddScoped<IAutenticadorCredenciales, AutenticacionLocal>();
            }
        }

        private static IHttpClientBuilder ConfigurarClienteApiCommon(IHttpClientBuilder builder) =>
            builder.ConfigureHttpClient((sp, http) =>
            {
                var opciones = sp.GetRequiredService<IOptions<OpcionesApiCommon>>().Value;

                if (!string.IsNullOrWhiteSpace(opciones.UrlBase))
                {
                    // La barra final importa: sin ella, Uri descarta el ultimo
                    // segmento de la ruta base al combinarla con la relativa.
                    http.BaseAddress = new Uri(opciones.UrlBase.TrimEnd('/') + "/");
                }

                if (!string.IsNullOrWhiteSpace(opciones.ApiKey))
                {
                    http.DefaultRequestHeaders.Add(opciones.NombreCabeceraApiKey, opciones.ApiKey);
                }
            });

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
