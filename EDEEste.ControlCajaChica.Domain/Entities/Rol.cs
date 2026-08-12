using System;
using System.Collections.Generic;
using System.Text;

namespace EDEEste.ControlCajaChica.Domain.Entities
{
    public class Rol
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public required string Nombre { get; set; }
    }
}
