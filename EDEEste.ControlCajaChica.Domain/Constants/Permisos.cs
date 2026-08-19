using System;
using System.Collections.Generic;

namespace EDEEste.ControlCajaChica.Domain.Constants
{
    /// <summary>
    /// Catálogo de permisos del sistema.
    ///
    /// Un rol dice quién es alguien; un permiso dice qué puede hacer. Las pantallas y
    /// endpoints se protegen siempre por permiso, nunca por rol: así, si mañana cambia
    /// quién aprueba una reposición, se toca solo el mapa de <see cref="PermisosPorRol"/>
    /// y no hay que salir a buscar atributos [Authorize(Roles = ...)] regados.
    ///
    /// El texto de cada constante es también el nombre de la política de autorización
    /// que se registra en Program.cs.
    /// </summary>
    public static class Permisos
    {
        // --- Gastos ---
        public const string VerGastos = "gastos.ver";
        public const string RegistrarGasto = "gastos.registrar";

        /// <summary>Revisar el gasto e inspeccionar sus comprobantes (Gerente).</summary>
        public const string RevisarGastos = "gastos.revisar";

        // --- Arqueos ---
        public const string VerArqueos = "arqueos.ver";
        public const string EjecutarArqueo = "arqueos.ejecutar";

        // --- Reposiciones ---
        public const string VerReposiciones = "reposiciones.ver";
        public const string SolicitarReposicion = "reposiciones.solicitar";
        public const string AprobarReposicion = "reposiciones.aprobar";
        public const string PagarReposicion = "reposiciones.pagar";

        /// <summary>Descargar el expediente PDF consolidado de una reposición.</summary>
        public const string DescargarExpediente = "reposiciones.expediente";

        // --- Auditoría ---
        /// <summary>Historial de reposiciones, arqueos y reportes de descuadres.</summary>
        public const string ConsultarHistorial = "auditoria.historial";

        // --- Configuración ---
        public const string ConfigurarFondos = "config.fondos";
        public const string ConfigurarCategorias = "config.categorias";
        public const string AdministrarUsuarios = "config.usuarios";

        public static readonly IReadOnlyList<string> Todos =
        [
            VerGastos,
            RegistrarGasto,
            RevisarGastos,
            VerArqueos,
            EjecutarArqueo,
            VerReposiciones,
            SolicitarReposicion,
            AprobarReposicion,
            PagarReposicion,
            DescargarExpediente,
            ConsultarHistorial,
            ConfigurarFondos,
            ConfigurarCategorias,
            AdministrarUsuarios
        ];
    }
}
