# AGENTS.md

Blazor Server (`AddInteractiveServerComponents`) app on .NET 10 using Clean Architecture, implementing EDEEste's "caja chica" (petty cash) system. UI text, code comments, and README are in **Spanish** — write new code/comments in Spanish.

## Commands
- Build: `dotnet build EDEEste.ControlCajaChica.slnx`
- Run: `dotnet run --project EDEEste.ControlCajaChica.Presentation` (profile `http` → http://localhost:5055)
- No test project exists and there is no CI; `dotnet build` is the only verification. The solution uses the new `.slnx` XML format (not `.sln`).

## Known broken state
The solution currently does **not** compile: 27 CS1501 errors in `Presentation/Components/Account/Pages/Manage/*.razor`. Those pages call `RedirectManager.RedirectToInvalidUser(UserManager, HttpContext)`, but `IdentityRedirectManager.RedirectToInvalidUser` (`Presentation/Components/Account/IdentityRedirectManager.cs:52`) only accepts `HttpContext`. Fix the manager to match the template callers (or restore the stock .NET 10 template signature) — don't chase these as 27 separate bugs.

## Architecture
- Project references flow one way: Domain ← Application ← Infrastructure, and Presentation → Application + Infrastructure. Presentation is the only executable; keep the direction.
- Domain entities are the most implemented layer. Most `Application/Features/*` files are empty stubs (e.g. `RegistrarGastoCommand.cs` is a bare class) — the command/handler pattern is scaffolded, not implemented.
- Business rules live in `README.md` (Spanish): fondo fijo invariants, 2.5% max per gasto, reposición window 30–20% of fondo, and the role matrix (Custodio / Admin / Aprobador / Finanzas / Auditor).

## Gotchas
- Two `ApplicationDbContext` classes exist. The canonical one is `Infrastructure/Persistence/ApplicationDbContext` (registered in `Program.cs`). `Presentation/Data/ApplicationDbContext.cs` is an intentionally empty stub (migration code references the type) — don't re-enable it or duplicate the Infrastructure class there.
- EF migrations live under `Presentation/Data/Migrations` (only the stock Identity schema so far). New domain tables need a migration there. `IApplicationDbContext` only exposes `SaveChangesAsync`; the concrete DbContext is used for queries.
- `AuditoriaInterceptor` (`Infrastructure/Persistence/Interceptors/AuditoriaInterceptor.cs`) runs on every SaveChanges: fills `AuditableEntity` audit fields, turns physical deletes into soft deletes, writes JSON before/after rows to `LogsAuditoria`, and stamps `HashFirma` on `ITamperProofEntity`. It hardcodes the current user as `"AdminCajaChica"` — not wired to ASP.NET Identity.
- Global soft-delete query filters (`!IsDeleted`) are configured **only** for `Gasto` and `FondoCajaChica` in `ApplicationDbContext.OnModelCreating`. New `AuditableEntity` subclasses are soft-deletable but won't be filtered until you add a filter.
- `Infrastructure` services are not registered in DI: `CriptografiaService`, `QuestPdfReporteService`, `UsuarioService`, `FileStorageService`, and `AuditoriaInterceptor` itself are all missing from `Program.cs`. The DbContext constructor requires `AuditoriaInterceptor`, so any DbContext use fails at runtime until these are wired up.
- `CriptografiaService` embeds a hardcoded HMAC secret key; `QuestPdfReporteService` sets the QuestPDF Community license in code.
- No linters, formatters, or `opencode.json` config in the repo.
