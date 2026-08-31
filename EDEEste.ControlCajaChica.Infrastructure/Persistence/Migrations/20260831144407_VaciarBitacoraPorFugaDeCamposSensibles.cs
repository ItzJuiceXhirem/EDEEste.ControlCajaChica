using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EDEEste.ControlCajaChica.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VaciarBitacoraPorFugaDeCamposSensibles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Antes de este fix, AuditoriaInterceptor serializaba TODAS las columnas
            // de la fila que cambiaba -- incluidas PasswordHash y SecurityStamp de
            // AspNetUsers, y TokenReseteo de SolicitudesPasswordReset -- en
            // ValoresAnteriores/ValoresNuevos, en texto plano. Con el interceptor ya
            // corregido para redactar esas columnas, lo que quedaba grabado de antes
            // no se puede "des-redactar" fila por fila (romperia la cadena de hashes
            // de la bitacora), asi que se vacia entera. La siguiente escritura arranca
            // de nuevo desde LogAuditoria.HashGenesis.
            migrationBuilder.Sql("DELETE FROM LogsAuditoria;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Irreversible a proposito: los datos borrados (hashes de contrasena y
            // tokens de reseteo en claro) no deben recuperarse bajo ninguna
            // circunstancia, ni siquiera revirtiendo la migracion.
        }
    }
}
