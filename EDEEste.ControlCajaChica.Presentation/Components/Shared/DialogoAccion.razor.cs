using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Domain.Constants;
using Microsoft.AspNetCore.Components;

namespace EDEEste.ControlCajaChica.Presentation.Components.Shared
{
    /// <summary>Cómo se pinta el botón de confirmar: acción normal o acción destructiva.</summary>
    public enum EstiloDialogo
    {
        Normal,
        Peligro
    }

    public partial class DialogoAccion
    {
        private string motivo = string.Empty;
        private ElementReference campoMotivo;

        [Parameter, EditorRequired]
        public string Titulo { get; set; } = string.Empty;

        // Qué se está por hacer, en una o dos frases.
        [Parameter]
        public string? Texto { get; set; }

        [Parameter]
        public string TextoConfirmar { get; set; } = "Confirmar";

        [Parameter]
        public EstiloDialogo Estilo { get; set; } = EstiloDialogo.Normal;

        /// <summary>Pide un motivo escrito: el botón de confirmar no se habilita sin él.</summary>
        [Parameter]
        public bool RequiereMotivo { get; set; }

        [Parameter]
        public string? EtiquetaMotivo { get; set; }

        [Parameter]
        public string? MarcadorMotivo { get; set; }

        [Parameter]
        public int LongitudMaximaMotivo { get; set; } = LimitesReposicion.LongitudMaximaMotivo;

        /// <summary>
        /// Un motivo que ya existe y se muestra de solo lectura (el de Finanzas al
        /// rechazar una solicitud devuelta, por ejemplo). Puede convivir con
        /// <see cref="RequiereMotivo"/> cuando además hay que escribir uno nuevo.
        /// </summary>
        [Parameter]
        public string? MotivoFijo { get; set; }

        [Parameter]
        public string? EtiquetaMotivoFijo { get; set; }

        /// <summary>Mientras la acción se procesa se bloquea todo, para no confirmarla dos veces.</summary>
        [Parameter]
        public bool Procesando { get; set; }

        /// <summary>
        /// Recibe el motivo escrito (sin espacios sobrantes), o null si el diálogo no lo
        /// pide. El servidor lo vuelve a validar: este botón deshabilitado es comodidad,
        /// no seguridad.
        /// </summary>
        [Parameter, EditorRequired]
        public EventCallback<string?> OnConfirmar { get; set; }

        [Parameter, EditorRequired]
        public EventCallback OnCancelar { get; set; }

        private bool PuedeConfirmar =>
            !Procesando && (!RequiereMotivo || !string.IsNullOrWhiteSpace(motivo));

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender && RequiereMotivo)
            {
                // Un fallo de JS interop dentro de un circuito de Blazor Server lo tumba
                // entero: que el foco no llegue al campo no justifica perder la pantalla.
                try
                {
                    await campoMotivo.FocusAsync();
                }
                catch
                {
                }
            }
        }

        private Task ConfirmarAsync() =>
            PuedeConfirmar
                ? OnConfirmar.InvokeAsync(RequiereMotivo ? motivo.Trim() : null)
                : Task.CompletedTask;

        private Task CancelarAsync() =>
            Procesando ? Task.CompletedTask : OnCancelar.InvokeAsync();
    }
}
