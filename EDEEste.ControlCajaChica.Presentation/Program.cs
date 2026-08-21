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
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;

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

// DbContext, interceptores de auditoría/integridad, criptografía, repositorios e
// IIdentityService.
builder.Services.AddInfrastructure(builder.Configuration);

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
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// Va después de AddIdentityCore a propósito: este registro sustituye al que aquel
// agrega por defecto (que solo mira si el correo está confirmado).
builder.Services.AddScoped<IUserConfirmation<Usuario>, ConfirmacionAccesoUsuario>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

// Descarga del expediente PDF consolidado de una reposición.
app.MapReposicionEndpoints();

// Los roles del catálogo se crean al arrancar si aún no existen (operación idempotente).
await using (var scope = app.Services.CreateAsyncScope())
{
    var inicializador = scope.ServiceProvider.GetRequiredService<InicializadorIdentidad>();
    await inicializador.SembrarRolesAsync();
}

app.Run();
