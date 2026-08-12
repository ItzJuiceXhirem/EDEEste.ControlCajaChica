// ApplicationUser is deprecated in favor of Domain.Entities.Usuario. Keep a tiny wrapper for compatibility if other files reference the type.
using Microsoft.AspNetCore.Identity;
using EDEEste.ControlCajaChica.Domain.Entities;

namespace EDEEste.ControlCajaChica.Presentation.Data
{
    [Obsolete("Use Domain.Entities.Usuario instead.")]
    public sealed class ApplicationUser : IdentityUser
    {
        public ApplicationUser() { }
        public ApplicationUser(Usuario u)
        {
            Id = u.Id;
            UserName = u.UserName;
            NormalizedUserName = u.NormalizedUserName;
            Email = u.Email;
            NormalizedEmail = u.NormalizedEmail;
        }
    }
}
