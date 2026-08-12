using System;
using Microsoft.AspNetCore.Identity;

namespace EDEEste.ControlCajaChica.Domain.Entities
{
    public class Usuario : IdentityUser
    {
        public string Nombre { get; set; } = string.Empty;
        public Rol Rol { get; set; }
        public bool Activo { get; set; } = true;
    }
}
