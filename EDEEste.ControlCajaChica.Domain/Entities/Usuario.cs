using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;

namespace EDEEste.ControlCajaChica.Domain.Entities
{
    public class Usuario
    {
        public required string Id { get; set; }
        public required string Nombre { get; set; }
        public string Email { get; set; } = string.Empty;
        public Rol RolId { get; set; } = new Rol();
        public bool Activo { get; set; }

    }
}
