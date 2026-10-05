using System;
using System.Collections.Generic;

namespace EDEEste.ControlCajaChica.Application.DTOs
{
    /// <summary>
    /// Los valores que de verdad aparecen en la bitácora, para llenar los filtros con
    /// opciones que siempre devuelven algo.
    /// </summary>
    public sealed class OpcionesFiltroBitacoraDto
    {
        public IReadOnlyList<string> Tablas { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> UsuarioIds { get; init; } = Array.Empty<string>();
    }
}
