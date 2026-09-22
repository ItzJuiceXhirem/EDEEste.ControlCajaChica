using System;

namespace EDEEste.ControlCajaChica.Infrastructure.Configuration
{
    /// <summary>
    /// Raíz de disco donde vive App_Data (comprobantes, expedientes de reposición y
    /// staging de subidas). Se resuelve en Program.cs a partir de
    /// IHostEnvironment.ContentRootPath y no de Directory.GetCurrentDirectory():
    /// bajo IIS el directorio de trabajo del proceso no siempre coincide con la
    /// carpeta de la aplicación, y con más archivos pasando por aquí (staging suma
    /// varias escrituras por gasto) un directorio equivocado deja de ser un bug
    /// silencioso para convertirse en uno frecuente.
    /// </summary>
    public sealed class OpcionesAlmacenamiento
    {
        public const string Seccion = "Almacenamiento";

        // Carpeta especial de ASP.NET Core que no se sirve por HTTP
        public const string NombreCarpeta = "App_Data";

        // Ruta absoluta a la carpeta App_Data
        public string RutaRaiz { get; set; } = string.Empty;

        /// <summary>
        /// Cuanto se conserva un archivo de staging sin promover antes de
        /// considerarlo abandonado. Un solo valor compartido por el barrido
        /// oportunista (GastoEndpoints, en cada subida) y el de respaldo
        /// (LimpiezaStagingBackgroundService) -- vive aquí y no en Presentation
        /// porque Infrastructure no puede referenciar esa capa.
        /// </summary>
        public static readonly TimeSpan RetencionStaging = TimeSpan.FromHours(24);
    }
}
