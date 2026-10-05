using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace EDEEste.ControlCajaChica.Application.Features.Auditoria
{
    public sealed record CambioDeCampo(string Campo, string? Antes, string? Despues);

    public sealed class ResultadoDiferencia
    {
        public static readonly ResultadoDiferencia Ilegible = new() { Legible = false };

        public IReadOnlyList<CambioDeCampo> Cambios { get; init; } = Array.Empty<CambioDeCampo>();

        // false si alguno de los dos JSON de la bitácora no se pudo leer.
        public bool Legible { get; init; } = true;
    }

    /// <summary>
    /// Qué cambió en una entrada de la bitácora. AuditoriaInterceptor guarda la fila
    /// COMPLETA antes y después (todas las columnas, aunque cambie una sola), y
    /// leerla así obliga a comparar a ojo decenas de campos. Esto deja solo los que
    /// difieren. En una creación no hay "antes", así que salen todos los campos con
    /// valor; en un borrado lógico, los que lo marcaron como borrado.
    ///
    /// No oculta nada: un campo técnico que cambió (HashFirma, FechaModificacion)
    /// también sale. En una herramienta de auditoría, decidir por el Auditor qué
    /// cambios no le interesan sería esconderle información.
    /// </summary>
    public static class DiferenciaDeValores
    {
        public static ResultadoDiferencia Calcular(string? valoresAnteriores, string? valoresNuevos)
        {
            if (!IntentarLeer(valoresAnteriores, out var antes) || !IntentarLeer(valoresNuevos, out var despues))
            {
                return ResultadoDiferencia.Ilegible;
            }

            // Primero en el orden de la fila nueva, que es el de la entidad; después
            // los que solo estaban en la anterior.
            var campos = despues.Keys.Concat(antes.Keys.Where(campo => !despues.ContainsKey(campo)));

            var cambios = campos
                .Select(campo => new CambioDeCampo(campo, antes.GetValueOrDefault(campo), despues.GetValueOrDefault(campo)))
                .Where(cambio => !string.Equals(cambio.Antes, cambio.Despues, StringComparison.Ordinal))
                .ToList();

            return new ResultadoDiferencia { Cambios = cambios };
        }

        /* Un JSON vacío o ausente es legítimo (una creación no tiene valores
           anteriores) y se lee como "sin campos". Uno que no se puede interpretar se
           informa como ilegible en vez de lanzar: una fila dañada de la bitácora no
           debe tumbar la pantalla que existe precisamente para revisarla. */
        private static bool IntentarLeer(string? json, out Dictionary<string, string?> valores)
        {
            valores = new Dictionary<string, string?>(StringComparer.Ordinal);

            if (string.IsNullOrWhiteSpace(json))
            {
                return true;
            }

            try
            {
                using var documento = JsonDocument.Parse(json);
                if (documento.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                foreach (var propiedad in documento.RootElement.EnumerateObject())
                {
                    valores[propiedad.Name] = Texto(propiedad.Value);
                }

                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        // El texto tal cual sin comillas; números, fechas y booleanos como vienen en el JSON.
        private static string? Texto(JsonElement valor) => valor.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => valor.GetString(),
            _ => valor.GetRawText()
        };
    }
}
