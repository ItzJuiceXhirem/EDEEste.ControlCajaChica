using System;

namespace EDEEste.ControlCajaChica.Domain.Interfaces
{
    /// <summary>
    /// Entidad con sello criptografico de inmutabilidad: al guardarla se calcula un
    /// HMAC sobre sus campos criticos y al leerla se vuelve a calcular para detectar
    /// si alguien modifico la fila directamente en la base de datos.
    /// </summary>
    public interface ITamperProofEntity
    {
        /// <summary>HMAC-SHA256 de <see cref="ObtenerCadenaParaHash"/>. Se persiste.</summary>
        string HashFirma { get; set; }

        /// <summary>
        /// Resultado de revalidar la firma al materializar la entidad desde la BDD.
        /// No se persiste (cada implementacion la marca con [NotMapped]): es un dato
        /// de solo runtime. Arranca en true para entidades nuevas, que aun no tienen
        /// firma con la cual comparar.
        /// </summary>
        bool IntegridadVerificada { get; set; }

        /// <summary>
        /// Cadena canonica con los campos que la firma protege. Debe construirse con
        /// <see cref="Common.ConstructorFirma"/> para que sea estable entre culturas
        /// y entre escritura y relectura.
        /// </summary>
        string ObtenerCadenaParaHash();
    }
}
