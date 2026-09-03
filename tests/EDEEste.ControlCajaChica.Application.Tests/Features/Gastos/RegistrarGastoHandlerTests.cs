using System;
using System.IO;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Features.Gastos;
using EDEEste.ControlCajaChica.Application.Tests.TestDoubles;
using EDEEste.ControlCajaChica.Domain.Entities;
using Xunit;

namespace EDEEste.ControlCajaChica.Application.Tests.Features.Gastos
{
    /// <summary>
    /// Cubre puntualmente las validaciones de RNC/Cedula y NCF (que se rechacen
    /// caracteres invalidos en vez de descartarlos en silencio, y que el NCF exija el
    /// formato exacto de la DGII), el tope de comprobantes por gasto, y que una
    /// referencia de staging invalida o ajena falle en vez de guardarse.
    ///
    /// La validacion de formato/MIME/magic-bytes de los archivos ya NO vive aqui --
    /// se movio a ValidadorComprobante (ver ValidadorComprobanteTests) y se aplica en
    /// el endpoint de subida, no en este handler: los comprobantes llegan como
    /// referencias a staging, nunca como streams.
    /// </summary>
    public class RegistrarGastoHandlerTests
    {
        // Debe coincidir con el valor por defecto de FakeCurrentUserService: es el
        // usuario "logueado" contra el que se resuelve la carpeta de staging.
        private const string UsuarioIdPrueba = "usuario-prueba";

        private static (FondoCajaChica fondo, CategoriaGasto categoria, FakeApplicationDbContext contexto, FakeFileStorageService almacenamiento, RegistrarGastoHandler handler)
            CrearEscenario()
        {
            var fondo = new FondoCajaChica { MontoFijo = 10000m, BalanceActual = 10000m, PorcentajeMaximoPorGasto = 100m };
            var categoria = new CategoriaGasto { Nombre = "Combustible", RequiereNCF = false, Activo = true };

            var fondos = new FakeFondoRepository();
            fondos.Agregar(fondo);

            var categorias = new FakeCategoriaGastoRepository();
            categorias.Agregar(categoria);

            var contexto = new FakeApplicationDbContext();
            var almacenamiento = new FakeFileStorageService();

            var handler = new RegistrarGastoHandler(
                fondos, categorias, new FakeGastoRepository(), almacenamiento,
                new FakeCurrentUserService(), new FakeIdentityService(), new FakeAutorizacionService(), contexto);

            return (fondo, categoria, contexto, almacenamiento, handler);
        }

        /// <summary>
        /// Sube un comprobante de prueba a staging (como lo haria GastoEndpoints) y
        /// arma el comando apuntando a esa referencia -- el handler ya no acepta
        /// contenido de archivo directo.
        /// </summary>
        private static async Task<RegistrarGastoCommand> ComandoBaseAsync(
            FondoCajaChica fondo, CategoriaGasto categoria, FakeFileStorageService almacenamiento)
        {
            var referencia = await almacenamiento.GuardarComprobanteEnStagingAsync(
                UsuarioIdPrueba, "factura.pdf", ".pdf", new MemoryStream("%PDF-1.4"u8.ToArray()));

            return new RegistrarGastoCommand
            {
                FondoCajaChicaId = fondo.Id,
                CategoriaGastoId = categoria.Id,
                Proveedor = "Proveedor de Prueba",
                RNCProveedor = "123456789",
                NCF = string.Empty,
                Subtotal = 100m,
                MontoITBIS = 0m,
                MontoTotal = 100m,
                FechaGasto = DateTime.Today,
                Comprobantes =
                [
                    new RegistrarGastoCommand.ComprobanteEntrada
                    {
                        Referencia = referencia,
                        Descripcion = "Factura de prueba"
                    }
                ]
            };
        }

        [Fact]
        public async Task Rnc_ConLetras_Falla()
        {
            var (fondo, categoria, contexto, almacenamiento, handler) = CrearEscenario();
            var comando = await ComandoBaseAsync(fondo, categoria, almacenamiento);
            comando.RNCProveedor = "12A456789";

            var resultado = await handler.EjecutarAsync(comando);

            Assert.False(resultado.Exitoso);
            Assert.Contains(resultado.Errores, e => e.Contains("solo puede contener numeros"));
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Rnc_SoloDigitosConLongitudValida_Pasa()
        {
            var (fondo, categoria, contexto, almacenamiento, handler) = CrearEscenario();
            var comando = await ComandoBaseAsync(fondo, categoria, almacenamiento);
            comando.RNCProveedor = "12345678901"; // 11 digitos: cedula

            var resultado = await handler.EjecutarAsync(comando);

            Assert.True(resultado.Exitoso, string.Join("; ", resultado.Errores));
            Assert.Equal(1, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Rnc_ConGuionesDeCedula_Pasa()
        {
            // Los guiones son formato que agrega la pantalla (XXX-XXXXXXX-X), no algo
            // que el usuario escriba a mano; el handler los acepta y los descarta.
            var (fondo, categoria, contexto, almacenamiento, handler) = CrearEscenario();
            var comando = await ComandoBaseAsync(fondo, categoria, almacenamiento);
            comando.RNCProveedor = "123-4567890-1";

            var resultado = await handler.EjecutarAsync(comando);

            Assert.True(resultado.Exitoso, string.Join("; ", resultado.Errores));
            Assert.Equal(1, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Ncf_ConPrefijoInvalido_Falla()
        {
            var (fondo, categoria, contexto, almacenamiento, handler) = CrearEscenario();
            var comando = await ComandoBaseAsync(fondo, categoria, almacenamiento);
            comando.NCF = "A0100000001"; // no empieza con B ni E

            var resultado = await handler.EjecutarAsync(comando);

            Assert.False(resultado.Exitoso);
            Assert.Contains(resultado.Errores, e => e.Contains("El NCF no es valido"));
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Ncf_ConLongitudInvalida_Falla()
        {
            var (fondo, categoria, contexto, almacenamiento, handler) = CrearEscenario();
            var comando = await ComandoBaseAsync(fondo, categoria, almacenamiento);
            comando.NCF = "B010000000"; // 10 caracteres, deberia ser 11

            var resultado = await handler.EjecutarAsync(comando);

            Assert.False(resultado.Exitoso);
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Ncf_ValidoTipoPapel_Pasa()
        {
            var (fondo, categoria, contexto, almacenamiento, handler) = CrearEscenario();
            var comando = await ComandoBaseAsync(fondo, categoria, almacenamiento);
            comando.NCF = "B0100000001"; // 11 caracteres

            var resultado = await handler.EjecutarAsync(comando);

            Assert.True(resultado.Exitoso, string.Join("; ", resultado.Errores));
            Assert.Equal(1, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Ncf_ValidoTipoElectronico_Pasa()
        {
            var (fondo, categoria, contexto, almacenamiento, handler) = CrearEscenario();
            var comando = await ComandoBaseAsync(fondo, categoria, almacenamiento);
            comando.NCF = "E312345678901"; // 13 caracteres

            var resultado = await handler.EjecutarAsync(comando);

            Assert.True(resultado.Exitoso, string.Join("; ", resultado.Errores));
            Assert.Equal(1, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Itbis_IgualAlSubtotal_Falla()
        {
            var (fondo, categoria, contexto, almacenamiento, handler) = CrearEscenario();
            var comando = await ComandoBaseAsync(fondo, categoria, almacenamiento);
            comando.Subtotal = 100m;
            comando.MontoITBIS = 100m;
            comando.MontoTotal = 200m;

            var resultado = await handler.EjecutarAsync(comando);

            Assert.False(resultado.Exitoso);
            Assert.Contains(resultado.Errores, e => e.Contains("El ITBIS debe ser menor que el subtotal"));
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Itbis_MayorQueElSubtotal_Falla()
        {
            var (fondo, categoria, contexto, almacenamiento, handler) = CrearEscenario();
            var comando = await ComandoBaseAsync(fondo, categoria, almacenamiento);
            comando.Subtotal = 100m;
            comando.MontoITBIS = 150m;
            comando.MontoTotal = 250m;

            var resultado = await handler.EjecutarAsync(comando);

            Assert.False(resultado.Exitoso);
            Assert.Contains(resultado.Errores, e => e.Contains("El ITBIS debe ser menor que el subtotal"));
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Guardado_ConConflictoDeConcurrencia_FallaYNoCuentaComoGuardado()
        {
            var (fondo, categoria, contexto, almacenamiento, handler) = CrearEscenario();
            var comando = await ComandoBaseAsync(fondo, categoria, almacenamiento);
            contexto.FallarPorConcurrencia = true;

            var resultado = await handler.EjecutarAsync(comando);

            Assert.False(resultado.Exitoso);
            Assert.Contains(resultado.Errores, e => e.Contains("Otro usuario modificó"));
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Itbis_MenorQueElSubtotal_Pasa()
        {
            var (fondo, categoria, contexto, almacenamiento, handler) = CrearEscenario();
            var comando = await ComandoBaseAsync(fondo, categoria, almacenamiento);
            comando.Subtotal = 100m;
            comando.MontoITBIS = 18m;
            comando.MontoTotal = 118m;

            var resultado = await handler.EjecutarAsync(comando);

            Assert.True(resultado.Exitoso, string.Join("; ", resultado.Errores));
            Assert.Equal(1, contexto.VecesGuardado);
        }

        /// <summary>
        /// Antes vivia del lado del cliente (GetMultipleFiles(maximumFileCount: 20),
        /// que ademas lanzaba si se pasaba). Ahora es una regla de Validar(), sin
        /// ninguna carrera posible: una lista, un comando, una transaccion.
        /// </summary>
        [Fact]
        public async Task MasDe20Comprobantes_Falla()
        {
            var (fondo, categoria, contexto, almacenamiento, handler) = CrearEscenario();
            var comando = await ComandoBaseAsync(fondo, categoria, almacenamiento);

            // ComandoBaseAsync ya agrego uno; con 20 mas quedan 21 en total.
            for (var i = 0; i < 20; i++)
            {
                comando.Comprobantes.Add(new RegistrarGastoCommand.ComprobanteEntrada
                {
                    Referencia = Guid.NewGuid(),
                    Descripcion = $"Comprobante extra {i}"
                });
            }

            var resultado = await handler.EjecutarAsync(comando);

            Assert.False(resultado.Exitoso);
            Assert.Contains(resultado.Errores, e => e.Contains("No se pueden adjuntar mas de 20"));
            Assert.Equal(0, contexto.VecesGuardado);
        }

        /// <summary>
        /// Una referencia que nunca se subio (o que el barrido de staging ya limpio)
        /// falla en la Fase A -- antes de promover nada -- en vez de reventar mas
        /// adelante contra un manifiesto inexistente.
        /// </summary>
        [Fact]
        public async Task Comprobante_ConReferenciaInexistente_Falla()
        {
            var (fondo, categoria, contexto, almacenamiento, handler) = CrearEscenario();
            var comando = await ComandoBaseAsync(fondo, categoria, almacenamiento);
            comando.Comprobantes[0].Referencia = Guid.NewGuid(); // nunca se subio a staging

            var resultado = await handler.EjecutarAsync(comando);

            Assert.False(resultado.Exitoso);
            Assert.Contains(resultado.Errores, e => e.Contains("ya no está disponible"));
            Assert.Equal(0, contexto.VecesGuardado);
        }

        /// <summary>
        /// La carpeta de staging es por usuario: una referencia real, pero subida por
        /// otra persona, tiene que fallar igual que una inexistente -- es la misma
        /// comprobacion (LeerManifiestoStagingAsync compara el UsuarioId del
        /// manifiesto) la que cierra el IDOR de raiz.
        /// </summary>
        [Fact]
        public async Task Comprobante_SubidoPorOtroUsuario_Falla()
        {
            var (fondo, categoria, contexto, almacenamiento, handler) = CrearEscenario();
            var comando = await ComandoBaseAsync(fondo, categoria, almacenamiento);

            var referenciaAjena = await almacenamiento.GuardarComprobanteEnStagingAsync(
                "otro-usuario", "factura.pdf", ".pdf", new MemoryStream("%PDF-1.4"u8.ToArray()));
            comando.Comprobantes[0].Referencia = referenciaAjena;

            var resultado = await handler.EjecutarAsync(comando);

            Assert.False(resultado.Exitoso);
            Assert.Contains(resultado.Errores, e => e.Contains("ya no está disponible"));
            Assert.Equal(0, contexto.VecesGuardado);
        }
    }
}
