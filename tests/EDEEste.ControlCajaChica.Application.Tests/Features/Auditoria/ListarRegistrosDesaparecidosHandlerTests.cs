using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Features.Auditoria;
using EDEEste.ControlCajaChica.Application.Tests.TestDoubles;
using EDEEste.ControlCajaChica.Domain.Entities;
using Xunit;

namespace EDEEste.ControlCajaChica.Application.Tests.Features.Auditoria
{
    public class ListarRegistrosDesaparecidosHandlerTests
    {
        private const string TablaVerificable = "Gastos";

        // Una tabla en la que borrar es legítimo: el verificador no la declara.
        private const string TablaNoVerificable = "AspNetUserRoles";

        private readonly FakeBitacoraRepository _bitacora = new();
        private readonly FakeVerificadorIntegridadDatos _verificador = new();
        private long _secuencia;

        private void Registrar(string tabla, string registroId, string accion) =>
            _bitacora.Agregar(new List<LogAuditoria>
            {
                new()
                {
                    Secuencia = ++_secuencia,
                    NombreTabla = tabla,
                    RegistroId = registroId,
                    TipoAccion = accion,
                    UsuarioId = "usuario-prueba",
                    FechaEjecucion = DateTime.UtcNow
                }
            });

        private ListarRegistrosDesaparecidosHandler Handler(bool tienePermiso = true) =>
            new(_bitacora, _verificador, new FakeAutorizacionService(tienePermiso));

        [Fact]
        public async Task SoloAparecenLosRegistrosMencionadosQueYaNoEstanEnSuTabla()
        {
            var existente = Guid.NewGuid();
            var borrado = Guid.NewGuid();
            _verificador.IdsExistentes[TablaVerificable] = new HashSet<Guid> { existente };

            Registrar(TablaVerificable, existente.ToString(), "Added");
            Registrar(TablaVerificable, borrado.ToString(), "Added");
            Registrar(TablaVerificable, borrado.ToString(), "Modified");

            var resultado = await Handler().EjecutarAsync();

            Assert.True(resultado.Exitoso, string.Join("; ", resultado.Errores));
            var desaparecido = Assert.Single(resultado.Valor!);
            Assert.Equal(borrado.ToString(), desaparecido.RegistroId);
            // Se informa la ÚLTIMA vez que la bitácora lo mencionó.
            Assert.Equal("Modified", desaparecido.TipoAccion);
            Assert.Equal(3, desaparecido.Secuencia);
        }

        [Fact]
        public async Task UnRegistroEliminadoLogicamenteNoCuentaComoDesaparecido()
        {
            // El verificador incluye los eliminados lógicamente entre los existentes: la
            // fila sigue en la tabla, marcada IsDeleted.
            var eliminado = Guid.NewGuid();
            _verificador.IdsExistentes[TablaVerificable] = new HashSet<Guid> { eliminado };
            Registrar(TablaVerificable, eliminado.ToString(), "Deleted");

            var resultado = await Handler().EjecutarAsync();

            Assert.Empty(resultado.Valor!);
        }

        [Fact]
        public async Task TablasNoVerificablesYClavesQueNoSonGuid_SeIgnoran()
        {
            _verificador.IdsExistentes[TablaVerificable] = new HashSet<Guid>();
            Registrar(TablaNoVerificable, Guid.NewGuid().ToString(), "Deleted");
            Registrar(TablaVerificable, "N/A", "Added");

            var resultado = await Handler().EjecutarAsync();

            Assert.True(resultado.Exitoso);
            Assert.Empty(resultado.Valor!);
        }

        [Fact]
        public async Task SinPermiso_Falla()
        {
            var resultado = await Handler(tienePermiso: false).EjecutarAsync();

            Assert.False(resultado.Exitoso);
        }
    }
}
