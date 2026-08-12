using System;
using System.Collections.Generic;
using System.Text;

namespace EDEEste.ControlCajaChica.Domain.Entities
{
    public class CategoriaGasto
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public required string Nombre { get; set; }
        //public string CuentaContable { get; set; } = string.Empty;
        public bool RequiereNCF { get; set; }
        public bool Activo { get; set; }
    }
}
