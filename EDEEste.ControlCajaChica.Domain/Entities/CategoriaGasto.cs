using System;
using System.Collections.Generic;
using System.Text;

namespace EDEEste.ControlCajaChica.Domain.Entities
{
    public class CategoriaGasto : AuditableEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Nombre { get; set; } = string.Empty;
        public string CuentaContable { get; set; } = string.Empty;
        public bool RequiereNCF { get; set; }
        public bool Activo { get; set; }
    }
}
