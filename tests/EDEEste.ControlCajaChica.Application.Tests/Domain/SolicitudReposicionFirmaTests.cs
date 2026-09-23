using System;
using EDEEste.ControlCajaChica.Domain.Common;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;
using Xunit;

namespace EDEEste.ControlCajaChica.Application.Tests.Domain
{
    /// <summary>
    /// HashPdfConsolidado entra en la firma solo si existe. Si alguien lo volviera
    /// incondicional, todas las solicitudes firmadas antes de ese campo dejarian de
    /// validar y la app las marcaria como manipuladas -- esta prueba lo impide.
    /// </summary>
    public class SolicitudReposicionFirmaTests
    {
        [Fact]
        public void SinHashDelExpediente_LaCadenaEsLaMismaQueAntesDelCampo()
        {
            var solicitud = SolicitudDeEjemplo();

            Assert.Equal(CadenaAnteriorAlCampo(solicitud), solicitud.ObtenerCadenaParaHash());
        }

        [Fact]
        public void ConHashDelExpediente_LaCadenaLoIncluye()
        {
            var solicitud = SolicitudDeEjemplo();
            var sinHash = solicitud.ObtenerCadenaParaHash();

            solicitud.HashPdfConsolidado = new string('A', 64);

            Assert.NotEqual(sinHash, solicitud.ObtenerCadenaParaHash());
        }

        private static SolicitudReposicion SolicitudDeEjemplo() => new()
        {
            FondoCajaChicaId = Guid.NewGuid(),
            MontoReclamado = 1500m,
            FechaSolicitud = new DateTime(2026, 9, 1, 10, 30, 0, DateTimeKind.Utc),
            SolicitoUsuarioId = "usuario-custodio",
            RutaPdfConsolidado = "uploads/reposiciones/reposicion.pdf",
            Estado = EstadoReposicion.PendienteAprobacion
        };

        // Copia literal de ObtenerCadenaParaHash() tal como era antes de
        // HashPdfConsolidado. No se debe "actualizar" junto con la entidad: es el
        // formato con el que ya hay solicitudes firmadas en la BDD.
        private static string CadenaAnteriorAlCampo(SolicitudReposicion s) =>
            new ConstructorFirma(nameof(SolicitudReposicion))
                .Agregar(s.Id)
                .Agregar(s.FondoCajaChicaId)
                .Agregar(s.MontoReclamado)
                .Agregar(s.FechaSolicitud)
                .Agregar(s.SolicitoUsuarioId)
                .Agregar(s.GerenteUsuarioId)
                .Agregar(s.FechaAprobacion)
                .Agregar(s.FinanzasUsuarioId)
                .Agregar(s.FechaPago)
                .Agregar(s.ReferenciaPago)
                .Agregar(s.RutaPdfConsolidado)
                .Agregar((long)s.Estado)
                .Agregar(s.IsDeleted)
                .ToString();
    }
}
