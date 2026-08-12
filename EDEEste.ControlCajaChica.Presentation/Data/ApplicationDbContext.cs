// This file is intentionally left to preserve any Presentation-specific DbContext usage.
// The canonical Identity DbContext is in Infrastructure.Persistence.ApplicationDbContext and is used by the app.
// Remove this file if you prefer a single DbContext implementation in Infrastructure.
/*using Microsoft.EntityFrameworkCore;
using EDEEste.ControlCajaChica.Infrastructure.Persistence;
using EDEEste.ControlCajaChica.Infrastructure.Persistence.Interceptors;

namespace EDEEste.ControlCajaChica.Presentation.Data
{*/
    // Adapter that forwards the Presentation ApplicationDbContext symbol to the Infrastructure implementation.
    // This type exists so generated migration code compiled under the Presentation assembly that references
    // ApplicationDbContext continues to compile. The real DbContext implementation lives in Infrastructure.
   /* public class ApplicationDbContext : EDEEste.ControlCajaChica.Infrastructure.Persistence.ApplicationDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, AuditoriaInterceptor interceptor)
            : base(options as DbContextOptions<EDEEste.ControlCajaChica.Infrastructure.Persistence.ApplicationDbContext>, interceptor)
        {
        }
    }
}*/
