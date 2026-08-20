using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EDEEste.ControlCajaChica.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Dos cambios que van juntos porque los dos alteran la firma HMAC de su entidad
    /// y, por tanto, obligan a la misma limpieza de datos:
    ///
    ///  - Gastos.MotivoAnulacion: por que se anulo un gasto. Entra en
    ///    Gasto.ObtenerCadenaParaHash, asi que las filas anteriores a esta migracion
    ///    dejan de validar.
    ///  - Fondos.PorcentajeMaximoPorGasto: el tope por gasto deja de ser una constante
    ///    del codigo (2.5%) y pasa a configurarse por fondo. Entra en
    ///    FondoCajaChica.ObtenerCadenaParaHash, con el mismo efecto.
    ///
    /// No se incluye ninguna rutina que vuelva a firmar las filas viejas: reescribir
    /// firmas sobre datos ya guardados es justo lo que IntegridadComprometidaException
    /// existe para impedir. Al aplicar esto en un entorno con datos, hay que vaciar
    /// Gastos y Fondos (y la bitacora completa, que es una cadena de hashes).
    ///
    /// El control de concurrencia optimista (IsConcurrencyToken sobre
    /// FondoCajaChica.BalanceActual, SolicitudReposicion.Estado y Gasto.Estado) tambien
    /// entro en el modelo con este cambio, pero no aparece aqui: solo altera la
    /// clausula WHERE que EF genera en los UPDATE, no el esquema. Queda registrado en
    /// el snapshot, que es lo que importa para las migraciones futuras.
    /// </summary>
    public partial class AgregarMotivoAnulacionYTopePorGasto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MotivoAnulacion",
                table: "Gastos",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            // defaultValue 2.5 y no 0: EF genera 0 por ser el default del tipo, pero un
            // tope del 0% dejaria a los fondos existentes rechazando cualquier gasto.
            // 2.5 es el valor del README, que es el que regia antes de esta migracion,
            // asi que las filas que ya existen conservan exactamente su comportamiento.
            migrationBuilder.AddColumn<decimal>(
                name: "PorcentajeMaximoPorGasto",
                table: "Fondos",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 2.5m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MotivoAnulacion",
                table: "Gastos");

            migrationBuilder.DropColumn(
                name: "PorcentajeMaximoPorGasto",
                table: "Fondos");
        }
    }
}
