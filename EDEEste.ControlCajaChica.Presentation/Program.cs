using EDEEste.ControlCajaChica.Presentation.Authorization;
using EDEEste.ControlCajaChica.Presentation.Components;
using EDEEste.ControlCajaChica.Presentation.Components.Account;
using EDEEste.ControlCajaChica.Presentation.Endpoints;
using EDEEste.ControlCajaChica.Presentation.Services;
using EDEEste.ControlCajaChica.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using EDEEste.ControlCajaChica.Infrastructure;
using EDEEste.ControlCajaChica.Infrastructure.Identity;
using EDEEste.ControlCajaChica.Infrastructure.Persistence;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Features.Gastos;
using EDEEste.ControlCajaChica.Application.Features.Reposiciones;
using EDEEste.ControlCajaChica.Application.Features.Arqueos;
using EDEEste.ControlCajaChica.Application.Features.Fondos;
using EDEEste.ControlCajaChica.Application.Features.Categorias;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

// Una política por permiso del catálogo. Las páginas se protegen siempre por
// permiso ([Authorize(Policy = Permisos.X)]) y nunca por rol, para que la matriz de
// accesos viva en un único sitio: Domain/Constants/PermisosPorRol.cs.
builder.Services.AddAuthorization(opciones =>
{
    foreach (var permiso in Permisos.Todos)
    {
        opciones.AddPolicy(permiso, politica => politica.AddRequirements(new PermisoRequirement(permiso)));
    }
});

builder.Services.AddScoped<IAuthorizationHandler, PermisoAuthorizationHandler>();

// Quien es el usuario actual solo se sabe desde la capa web (HttpContext / circuito
// de Blazor), así que la implementación de ICurrentUserService se registra aquí y no
// en Infrastructure.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Defensa en profundidad: los handlers de dinero repiten la comprobacion de
// permiso que ya hace [Authorize(Policy = ...)] en la pantalla, para no depender
// solo de que la UI los haya protegido bien.
builder.Services.AddScoped<IAutorizacionService, AutorizacionService>();

// DbContext, interceptores de auditoría/integridad, criptografía, repositorios e
// IIdentityService.
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.ContentRootPath);

// Los casos de uso viven en Application, pero se registran aquí: Application no
// referencia ningún paquete (ni siquiera el de DI) a propósito, y Presentation es el
// composition root de la solución.
builder.Services.AddScoped<RegistrarGastoHandler>();
builder.Services.AddScoped<SolicitarAnulacionGastoHandler>();
builder.Services.AddScoped<AnularGastoHandler>();
builder.Services.AddScoped<RevertirAnulacionGastoHandler>();
builder.Services.AddScoped<CrearSolicitudReposicionHandler>();
builder.Services.AddScoped<AprobarReposicionHandler>();
builder.Services.AddScoped<ProcesarPagoReposicionHandler>();
builder.Services.AddScoped<RegistrarArqueoMensualHandler>();

// Fondos
builder.Services.AddScoped<CrearFondoHandler>();
builder.Services.AddScoped<ActualizarParametrosFondoHandler>();

// Categorias
builder.Services.AddScoped<CrearCategoriaGastoHandler>();
builder.Services.AddScoped<ActualizarCategoriaGastoHandler>();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// AddIdentityCore (no AddIdentity): AddIdentity vuelve a llamar AddAuthentication y
// registra de nuevo los esquemas de cookie que ya agregó AddIdentityCookies más
// arriba, lo que revienta el arranque con "Scheme already exists: Identity.Application".
builder.Services.AddIdentityCore<Usuario>(options =>
    {
        // Se mantiene activo, pero ya no significa "confirmó su correo": lo que
        // decide es ConfirmacionAccesoUsuario, es decir que un Administrador haya
        // aprobado la cuenta. Ver esa clase para el porqué.
        options.SignIn.RequireConfirmedAccount = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;

        // El valor por defecto de Identity (6) es corto para una app que maneja
        // dinero en efectivo: explicito y no implicito, misma logica que el bloqueo
        // de mas abajo.
        options.Password.RequiredLength = LimitesContrasena.LongitudMinima;

        // Explicito y no el valor por defecto de Identity a propósito: es una
        // decisión de seguridad de esta app (ver Login.razor.cs), no un detalle que
        // deba quedar implícito en el framework.
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// Va después de AddIdentityCore a propósito: este registro sustituye al que aquel
// agrega por defecto (que solo mira si el correo está confirmado).
builder.Services.AddScoped<IUserConfirmation<Usuario>, ConfirmacionAccesoUsuario>();

// /Account/Register es el unico punto de toda la app que acepta peticiones sin
// autenticar: sin este limite, cualquiera podria automatizar la creacion de cuentas
// Pendiente (llenando la cola de aprobacion del Administrador) o "reservar" el
// usuario de otro empleado antes que esa persona se registre. El resto de la app ya
// esta detras de autenticacion, y el login tiene su propio limite (Identity
// Lockout, ver mas abajo).
builder.Services.AddRateLimiter(opciones =>
{
    opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    opciones.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(contexto =>
    {
        if (!HttpMethods.IsPost(contexto.Request.Method) ||
            !contexto.Request.Path.StartsWithSegments("/Account/Register"))
        {
            return RateLimitPartition.GetNoLimiter("sin-limite");
        }

        var ip = contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(15),
            QueueLimit = 0
        });
    });

    // Cuota anti-DoS de la subida de comprobantes a staging. Por Id de usuario y
    // no por IP: el NAT de la oficina pondria a toda la empresa en la misma
    // cubeta. Cae a la IP solo si por algun motivo no hubiera usuario resuelto
    // todavia -- no deberia pasar detras de RequireAuthorization, pero el
    // particionador no puede asumirlo.
    opciones.AddPolicy("subida-comprobantes", contexto =>
    {
        var clave = contexto.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? contexto.Connection.RemoteIpAddress?.ToString()
            ?? "desconocido";

        return RateLimitPartition.GetFixedWindowLimiter(clave, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 40,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0
        });
    });

    // Misma partición por usuario, pero mucho más estrecha: una foto de perfil se
    // cambia de vez en cuando, no es parte de ningún flujo de trabajo repetitivo
    // como cargar los comprobantes de un gasto.
    opciones.AddPolicy("subida-foto-perfil", contexto =>
    {
        var clave = contexto.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? contexto.Connection.RemoteIpAddress?.ToString()
            ?? "desconocido";

        return RateLimitPartition.GetFixedWindowLimiter(clave, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0
        });
    });
});

// El limite por defecto de multipart es 128 MB; un comprobante no debe superar
// los 10 MB que ya exige el endpoint de subida, asi que esto es solo el techo
// duro del servidor -- corta ANTES de que el endpoint llegue a ver el archivo
// completo, no reemplaza esa validacion.
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(opciones =>
    opciones.MultipartBodyLengthLimit = 11 * 1024 * 1024);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // El template por defecto trae esta linea; se habia quedado fuera.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// Cabeceras que el navegador respeta en cada respuesta, sin depender de que cada
// pagina se acuerde de ponerlas. nosniff evita que el navegador reinterprete un
// comprobante subido como si fuera HTML/script por su contenido; DENY evita que la
// app se cargue dentro de un <iframe> ajeno (clickjacking); Referrer-Policy evita
// que la URL completa (con Id de gastos/fondos) viaje como referrer hacia un enlace
// externo, por ejemplo el que abre un comprobante en una pestaña nueva.
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

app.UseRateLimiter();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

// Descarga del expediente PDF consolidado de una reposición.
app.MapReposicionEndpoints();

// Visualización de un comprobante adjunto de un gasto (imagen o PDF).
app.MapGastoEndpoints();

// Subida y visualización de la foto de perfil.
app.MapPerfilEndpoints();

// Los roles del catálogo se crean al arrancar si aún no existen (operación idempotente).
await using (var scope = app.Services.CreateAsyncScope())
{
    var inicializador = scope.ServiceProvider.GetRequiredService<InicializadorIdentidad>();
    await inicializador.SembrarRolesAsync();
}

app.Run();
