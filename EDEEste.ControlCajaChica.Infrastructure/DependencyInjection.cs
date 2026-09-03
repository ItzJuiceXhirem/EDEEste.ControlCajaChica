using System;
using System.IO;
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
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration,
            string rutaRaizContenido)
        {
            AgregarCriptografia(services, configuration);
            AgregarAlmacenamiento(services, rutaRaizContenido);
            AgregarPersistencia(services, configuration);
            AgregarAutenticacion(services, configuration);
            AgregarConfiguracionInicial(services, configuration);

            services.AddScoped<IIdentityService, IdentityService>();
            services.AddScoped<IPasswordResetService, PasswordResetService>();
            services.AddScoped<IReporteGastosService, QuestPdfReporteService>();
            services.AddScoped<IFileStorageService, FileStorageService>();
            services.AddScoped<IPdfConsolidadorService, PdfConsolidadorService>();
            services.AddScoped<InicializadorIdentidad>();
            services.AddHostedService<LimpiezaStagingBackgroundService>();

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
            services.AddScoped<IArqueoRepository, ArqueoRepository>();
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

        /// <summary>
        /// A diferencia de la clave HMAC, NO se falla el arranque si falta el token:
        /// solo importa mientras el sistema no tenga ningun Administrador, y exigirlo
        /// siempre rompería cualquier despliegue ya configurado que nunca lo
        /// necesito. La pantalla misma (ConfiguracionInicial.razor.cs) es quien se
        /// niega a mostrar el formulario si esta vacio.
        /// </summary>
        private static void AgregarConfiguracionInicial(IServiceCollection services, IConfiguration configuration) =>
            services.Configure<OpcionesConfiguracionInicial>(
                configuration.GetSection(OpcionesConfiguracionInicial.Seccion));

        /// <summary>
        /// rutaRaizContenido llega desde IHostEnvironment.ContentRootPath, resuelto
        /// en Program.cs -- Infrastructure no referencia los paquetes de Hosting
        /// solo para esto, recibe la ruta ya calculada.
        /// </summary>
        private static void AgregarAlmacenamiento(IServiceCollection services, string rutaRaizContenido) =>
            services.Configure<OpcionesAlmacenamiento>(
                opciones => opciones.RutaRaiz = Path.Combine(rutaRaizContenido, "App_Data"));

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

            // Singleton a proposito: solo envuelve la clave HMAC ya leida de
            // configuracion (inmutable durante la vida de la app) y no tiene ningun
            // estado por peticion. Los interceptores de EF (AuditoriaInterceptor,
            // IntegridadInterceptor) dependen de que este servicio sea Singleton para
            // poder serlo ellos tambien -- ver el comentario en AgregarPersistencia.
            services.AddSingleton<ICriptografiaService, CriptografiaService>();
        }

        private static void AgregarPersistencia(IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetSection("ConnectionStrings")["DefaultConnection"]
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            // Los interceptores son Singleton -- EF los guarda como parte de la clave
            // de cache de su proveedor de servicios interno, asi que necesitan ser
            // SIEMPRE la misma instancia o revienta con ManyServiceProvidersCreated
            // Warning pasadas ~20 peticiones (se probo con instancias Scoped, por dos
            // caminos distintos, y las dos lo dispararon). AuditoriaInterceptor no
            // puede entonces recibir ICurrentUserService (Scoped) por constructor; lee
            // el usuario actual de AmbientUsuarioActual en su lugar -- ver ese archivo.
            services.AddSingleton<IInterceptor, AuditoriaInterceptor>();
            services.AddSingleton<IInterceptor, IntegridadInterceptor>();

            services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
                options.UseSqlServer(connectionString)
                       .AddInterceptors(serviceProvider.GetServices<IInterceptor>()));

            services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        }
    }
}
