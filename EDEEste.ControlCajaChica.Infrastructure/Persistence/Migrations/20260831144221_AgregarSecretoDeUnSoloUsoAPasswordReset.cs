using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EDEEste.ControlCajaChica.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarSecretoDeUnSoloUsoAPasswordReset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaExpiracionSecreto",
                table: "SolicitudesPasswordReset",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HashSecreto",
                table: "SolicitudesPasswordReset",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FechaExpiracionSecreto",
                table: "SolicitudesPasswordReset");

            migrationBuilder.DropColumn(
                name: "HashSecreto",
                table: "SolicitudesPasswordReset");
        }
    }
}
