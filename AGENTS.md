# AGENTS.md

Blazor Server (`AddInteractiveServerComponents`) app on .NET 10 using Clean Architecture, implementing EDEEste's "caja chica" (petty cash) system. UI text, code comments, and README are in **Spanish** — write new code/comments in Spanish.

## Commands
- Build: `dotnet build EDEEste.ControlCajaChica.slnx`
- Run: `dotnet run --project EDEEste.ControlCajaChica.Presentation` (profile `http` → http://localhost:5055)
- No test project exists and there is no CI; `dotnet build` is the only verification. The solution uses the new `.slnx` XML format (not `.sln`).

## First-run setup (required)
The app **throws at startup** if the HMAC signing key is missing — this is deliberate (see "Sello de integridad"). Set it once per machine:

```
dotnet user-secrets set "Criptografia:ClaveHmac" "<base64 of 32+ random bytes>" --project EDEEste.ControlCajaChica.Presentation
```

Never put this key in `appsettings.json`; the whole anti-tamper scheme relies on it not being reachable from the database or the repo. Changing the key invalidates every existing `HashFirma`.

Then create the database:

```
dotnet ef database update --project EDEEste.ControlCajaChica.Infrastructure --startup-project EDEEste.ControlCajaChica.Presentation
```

## Architecture
- Project references flow one way: Domain ← Application ← Infrastructure, and Presentation → Application + Infrastructure. Presentation is the only executable; keep the direction.
- **Domain has zero PackageReferences on purpose.** Don't add any. The Identity user entity is `Infrastructure/Identity/Usuario.cs` (`Usuario : IdentityUser`), not Domain — other entities refer to users only by `string` id.
- Roles live **only** in ASP.NET Identity (`AspNetRoles`/`AspNetUserRoles`). `Domain/Constants/RolesApp.cs` holds the canonical role names and is the single source of truth for seeding and `[Authorize(Roles = ...)]`. There is no `Rol` enum or `Usuario.Rol` column — a previous version had both and they silently desynced.
- Infrastructure self-registers through `AddInfrastructure(IConfiguration)` (`Infrastructure/DependencyInjection.cs`): DbContext, interceptors, crypto, `IIdentityService`, PDF, file storage. `Program.cs` should not register Infrastructure types directly.
- `ICurrentUserService` is implemented in **Presentation** (`Presentation/Services/CurrentUserService.cs`), not Infrastructure — it needs `IHttpContextAccessor` + `AuthenticationStateProvider`. It checks both because HttpContext is null inside an interactive Blazor circuit and the provider throws outside one.
- Identity is wired with `AddIdentityCore` + `.AddRoles<IdentityRole>()`, **not** `AddIdentity`. `AddIdentity` re-registers the cookie schemes that `AddIdentityCookies()` already added and blows up at startup with `Scheme already exists: Identity.Application`.
- Business rules live in `README.md` (Spanish): fondo fijo invariants, 2.5% max per gasto, reposición window 30–20% of fondo, and the role matrix (Custodio / Admin / Aprobador / Finanzas / Auditor).
- Most `Application/Features/*` files are still empty stubs — the command/handler pattern is scaffolded, not implemented.

## Sello de integridad (HMAC)
Rows implementing `ITamperProofEntity` carry a `HashFirma` so direct edits in the database are detectable.

- `AuditoriaInterceptor` (write side) signs on save, and **refuses to save** an entity whose `IntegridadVerificada` is false, so the app can't launder a tampered row by re-signing it.
- `IntegridadInterceptor` (read side, `IMaterializationInterceptor`) re-validates on every materialization, sets `IntegridadVerificada`, and logs `Critical`. It deliberately does not throw — the auditor still needs to list tampered rows.
- `LogAuditoria` is a **hash chain**: each row signs the previous row's `HashFirma`, so deleting or reordering log rows breaks the chain. `Secuencia` is a DB identity column and defines chain order.
- Build hash strings only with `Domain/Common/ConstructorFirma` — it length-prefixes fields, forces `InvariantCulture`, and versions the scheme. Ad-hoc interpolation reintroduces culture and field-boundary bugs.
- All decimals are forced to `decimal(18,4)` in `OnModelCreating` to match `ConstructorFirma`'s `F4` normalization. If the column scale and the hash scale ever diverge, every read reports a false tamper alert.

## Persistence
- Migrations live in **`Infrastructure/Persistence/Migrations`**, next to the DbContext. Always pass `--project EDEEste.ControlCajaChica.Infrastructure --startup-project EDEEste.ControlCajaChica.Presentation`. (An earlier orphaned migration under `Presentation/Data` was invisible to EF because the DbContext is in another assembly — don't recreate that split.)
- Every FK is a real `Guid` with a separate navigation property (`Gasto.FondoCajaChicaId` + `Gasto.FondoCajaChica`). Declaring the FK as the navigated object makes EF emit a shadow column like `FondoCajaChicaIdId` and leaves the relation outside the HMAC signature.
- All relationships use `DeleteBehavior.Restrict`. Physical deletes should never happen (the interceptor soft-deletes); Restrict is the backstop so the database refuses rather than cascading away accounting evidence.
- Soft-delete query filters are applied to **all seven** `AuditableEntity` subclasses. Use `IgnoreQueryFilters()` for audit/history views.

## Gotchas
- The interceptor overrides **both** `SavingChanges` and `SavingChangesAsync`. Overriding only the sync one silently skips all auditing, since `IApplicationDbContext` exposes only `SaveChangesAsync`.
- Business string columns are still `nvarchar(max)` (`Proveedor`, `NCF`, `CodigoSolicitud`, `Nombre`, …). Worth adding `HasMaxLength` before this hits production, but the lengths are business decisions — don't guess them.
- No unique index on `SolicitudReposicion.CodigoSolicitud` / `ArqueoCaja.CodigoArqueo` / `Gasto.NCF`; add them once the uniqueness rules are confirmed.
- `QuestPdfReporteService` sets the QuestPDF Community license in code.
- Self-registration (`Register.razor`) always assigns `RolesApp.RolPorDefecto` (= `Auditor`, read-only). An Administrador must promote users; nobody can self-assign fund access.
- The `/Account` Razor pages are still the stock English template text; the rest of the app is Spanish.
- No linters, formatters, or `opencode.json` config in the repo.
