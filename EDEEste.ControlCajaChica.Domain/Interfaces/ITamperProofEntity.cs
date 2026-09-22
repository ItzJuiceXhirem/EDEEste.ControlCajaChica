using System;

namespace EDEEste.ControlCajaChica.Domain.Interfaces
{
    /// <summary>
    /// Entidad con sello criptográfico de inmutabilidad: al guardarla se calcula un
    /// HMAC sobre sus campos críticos y al leerla se vuelve a calcular para detectar
    /// si alguien modificó la fila directamente en la base de datos.
    /// </summary>
    public interface ITamperProofEntity
    {
        /// HMAC-SHA256 de <see cref="ObtenerCadenaParaHash"/>. Se persiste.
        string HashFirma { get; set; }

        /// <summary>
        /// Resultado de revalidar la firma al materializar la entidad desde la BDD.
        /// No se persiste (cada implementacion la marca con [NotMapped]): es un dato
        /// de solo runtime. Arranca en true para entidades nuevas, que aún no tienen
        /// firma con la cual comparar.
        /// </summary>
        bool IntegridadVerificada { get; set; }

        /// <summary>
        /// Cadena canónica con los campos que la firma protege. Debe construirse con
        /// <see cref="Common.ConstructorFirma"/> para que sea estable entre culturas
        /// y entre escritura y relectura.
        /// </summary>
        string ObtenerCadenaParaHash();
    }
}
