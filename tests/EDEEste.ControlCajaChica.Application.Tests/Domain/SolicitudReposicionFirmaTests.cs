using System;
using System.Linq;
using EDEEste.ControlCajaChica.Domain.Common;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;
using Xunit;

namespace EDEEste.ControlCajaChica.Application.Tests.Domain
{
    /// <summary>
    /// Los campos opcionales de la firma (hash del expediente y los tres motivos) entran
    /// solo hasta el ultimo que tenga valor, con la marca de nulo en los huecos de en
    /// medio (ConstructorFirma.AgregarOpcionalesAlFinal). Si alguien los volviera
    /// incondicionales, todas las solicitudes firmadas antes de esos campos dejarian de
    /// validar y la app las marcaria como manipuladas; si no se escribieran los huecos,
    /// un valor podria moverse de un campo a otro sin romper la firma. Estas pruebas
    /// impiden las dos cosas.
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

        [Fact]
        public void SoloConHashDelExpediente_LaCadenaEsLaAnteriorMasElHashAlFinal()
        {
            // Una solicitud con solo hash (como las creadas antes de los motivos) debe
            // producir la misma cadena que antes: si cambiara, su firma dejaria de validar.
            var solicitud = SolicitudDeEjemplo();
            var hash = new string('A', 64);
            solicitud.HashPdfConsolidado = hash;

            Assert.Equal(CadenaAnteriorAlCampo(solicitud) + "64:" + hash, solicitud.ObtenerCadenaParaHash());
        }

        [Fact]
        public void CadaMotivoCambiaLaCadena()
        {
            var sinMotivo = SolicitudDeEjemplo().ObtenerCadenaParaHash();

            var conDevolucion = SolicitudDeEjemplo();
            conDevolucion.MotivoDevolucion = "motivo";
            var conReaprobacion = SolicitudDeEjemplo();
            conReaprobacion.MotivoReaprobacion = "motivo";
            var conRechazo = SolicitudDeEjemplo();
            conRechazo.MotivoRechazo = "motivo";

            Assert.NotEqual(sinMotivo, conDevolucion.ObtenerCadenaParaHash());
            Assert.NotEqual(sinMotivo, conReaprobacion.ObtenerCadenaParaHash());
            Assert.NotEqual(sinMotivo, conRechazo.ObtenerCadenaParaHash());
        }

        [Fact]
        public void UnMotivoEnMedio_EscribeLaMarcaDeNuloDelHashQueFalta()
        {
            // Sin hash y con un motivo: el hueco del hash se escribe ("~:"), para que el
            // motivo quede en su propia posicion y no se confunda con el hash.
            var solicitud = SolicitudDeEjemplo();
            solicitud.MotivoDevolucion = "abc";

            Assert.Equal(CadenaAnteriorAlCampo(solicitud) + "~:" + "3:abc", solicitud.ObtenerCadenaParaHash());
        }

        [Fact]
        public void MoverUnValorEntreCamposOpcionales_CambiaLaCadena()
        {
            // Sin escribir los huecos, "hash=X" y "motivo=X" darian la misma cadena y
            // alguien con acceso a la base podria mover un valor de un campo a otro sin
            // romper la firma (por ejemplo, para apagar la verificacion del expediente).
            var comoHash = SolicitudDeEjemplo();
            comoHash.HashPdfConsolidado = "valor";

            var comoDevolucion = SolicitudDeEjemplo();
            comoDevolucion.MotivoDevolucion = "valor";

            var comoReaprobacion = SolicitudDeEjemplo();
            comoReaprobacion.MotivoReaprobacion = "valor";

            var comoRechazo = SolicitudDeEjemplo();
            comoRechazo.MotivoRechazo = "valor";

            var cadenas = new[]
            {
                comoHash.ObtenerCadenaParaHash(),
                comoDevolucion.ObtenerCadenaParaHash(),
                comoReaprobacion.ObtenerCadenaParaHash(),
                comoRechazo.ObtenerCadenaParaHash()
            };

            Assert.Equal(cadenas.Length, cadenas.Distinct().Count());
        }

        // Ids fijos a proposito: las pruebas que comparan la cadena de dos solicitudes
        // distintas solo miden lo que dicen medir si lo UNICO que cambia entre ellas es el
        // campo bajo prueba (con un Guid nuevo cada vez, siempre saldrian distintas).
        private static readonly Guid IdDeEjemplo = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid FondoDeEjemplo = Guid.Parse("22222222-2222-2222-2222-222222222222");

        private static SolicitudReposicion SolicitudDeEjemplo() => new()
        {
            Id = IdDeEjemplo,
            FondoCajaChicaId = FondoDeEjemplo,
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
