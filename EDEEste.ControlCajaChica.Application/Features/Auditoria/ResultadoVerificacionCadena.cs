using System;
using System.Collections.Generic;

namespace EDEEste.ControlCajaChica.Application.Features.Auditoria
{
    public enum TipoRupturaCadena
    {
        // La fila no coincide con su propia firma: alguien la editó en la base de datos.
        FirmaInvalida = 1,

        /* La fila no apunta a la firma de la anterior: alguien borró (o reordenó)
           filas justo antes de esta. En la primera fila de la cadena significa que
           se borró el principio. */
        EnlaceRoto = 2
    }

    public sealed record RupturaDeCadena(long Secuencia, Guid LogId, TipoRupturaCadena Tipo, DateTime FechaEjecucion);

    /// <summary>
    /// Resultado de recorrer la cadena de la bitácora completa.
    ///
    /// La cadena no puede detectar por sí sola que se borren sus ÚLTIMAS filas: lo que
    /// queda sigue siendo una cadena válida. Para eso existe la huella (el HashFirma
    /// de la última fila): el Auditor la anota fuera del sistema y, en la siguiente
    /// revisión, comprueba que siga dentro de la cadena.
    /// </summary>
    public sealed class ResultadoVerificacionCadena
    {
        public long TotalFilas { get; init; }
        public long? PrimeraSecuencia { get; init; }
        public long? UltimaSecuencia { get; init; }
        public DateTime? FechaUltimaFila { get; init; }

        // HashFirma de la última fila: lo que el Auditor anota para la próxima revisión.
        public string? HuellaActual { get; init; }

        // Las primeras rupturas encontradas (como máximo las que fija el handler).
        public IReadOnlyList<RupturaDeCadena> Rupturas { get; init; } = Array.Empty<RupturaDeCadena>();
        public int TotalRupturas { get; init; }

        // null si no se pidió comprobar una huella anterior.
        public bool? HuellaAnteriorEncontrada { get; init; }

        public bool CadenaIntegra => TotalRupturas == 0;
        public bool RupturasRecortadas => TotalRupturas > Rupturas.Count;
    }
}
