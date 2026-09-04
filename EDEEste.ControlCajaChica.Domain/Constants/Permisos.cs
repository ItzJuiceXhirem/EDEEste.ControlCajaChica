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

        /// <summary>
        /// Pedir la anulación de un gasto. No la ejecuta: lo deja pendiente de que el
        /// Gerente la confirme (Custodio).
        /// </summary>
        public const string SolicitarAnulacionGasto = "gastos.anular.solicitar";

        /// <summary>
        /// Anular un gasto, confirmar una anulación que pidió el Custodio, o
        /// revertirla. Es la misma decisión sobre el mismo expediente, así que es un
        /// solo permiso (Gerente).
        /// </summary>
        public const string AnularGasto = "gastos.anular";

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

        // --- Cuenta propia ---
        /// <summary>
        /// Gestionar los datos de la propia cuenta (hoy: la foto de perfil). Lo tienen
        /// todos los roles: no es una acción de negocio sujeta a segregación de
        /// funciones, sino algo que cualquiera puede hacer sobre sí mismo.
        ///
        /// Existe como permiso, y no como un RequireAuthorization() pelado, para que la
        /// matriz de accesos siga viviendo entera en <see cref="PermisosPorRol"/> --
        /// sin excepciones que haya que ir a buscar sueltas por los endpoints.
        /// </summary>
        public const string GestionarPerfilPropio = "perfil.gestionar";

        public static readonly IReadOnlyList<string> Todos =
        [
            VerGastos,
            RegistrarGasto,
            RevisarGastos,
            SolicitarAnulacionGasto,
            AnularGasto,
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
            AdministrarUsuarios,
            GestionarPerfilPropio
        ];
    }
}
