using System;

namespace EDEEste.ControlCajaChica.Infrastructure.Configuration
{
    /// <summary>
    /// Raiz de disco donde vive App_Data (comprobantes, expedientes de reposicion y
    /// staging de subidas). Se resuelve en Program.cs a partir de
    /// IHostEnvironment.ContentRootPath y no de Directory.GetCurrentDirectory():
    /// bajo IIS el directorio de trabajo del proceso no siempre coincide con la
    /// carpeta de la aplicacion, y con mas archivos pasando por aqui (staging suma
    /// varias escrituras por gasto) un directorio equivocado deja de ser un bug
    /// silencioso para convertirse en uno frecuente.
    /// </summary>
    public sealed class OpcionesAlmacenamiento
    {
        public const string Seccion = "Almacenamiento";

        /// <summary>Carpeta especial de ASP.NET Core que no se sirve por HTTP.</summary>
        public const string NombreCarpeta = "App_Data";

        /// <summary>Ruta absoluta a la carpeta App_Data.</summary>
        public string RutaRaiz { get; set; } = string.Empty;

        /// <summary>
        /// Cuanto se conserva un archivo de staging sin promover antes de
        /// considerarlo abandonado. Un solo valor compartido por el barrido
        /// oportunista (GastoEndpoints, en cada subida) y el de respaldo
        /// (LimpiezaStagingBackgroundService) -- vive aqui y no en Presentation
        /// porque Infrastructure no puede referenciar esa capa.
        /// </summary>
        public static readonly TimeSpan RetencionStaging = TimeSpan.FromHours(24);
    }
}
