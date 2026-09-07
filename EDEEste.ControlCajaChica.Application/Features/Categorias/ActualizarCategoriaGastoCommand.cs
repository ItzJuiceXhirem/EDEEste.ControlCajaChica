using System;

namespace EDEEste.ControlCajaChica.Application.Features.Categorias
{
    public sealed class ActualizarCategoriaGastoCommand
    {
        public Guid CategoriaGastoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string CuentaContable { get; set; } = string.Empty;
        public bool RequiereNCF { get; set; }
        public bool Activo { get; set; }
    }
}
