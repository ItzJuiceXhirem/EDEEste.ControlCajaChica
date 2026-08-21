using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EDEEste.ControlCajaChica.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarSolicitudesPasswordReset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SolicitudesPasswordReset",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    FechaSolicitud = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    TokenReseteo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FechaResolucion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResueltaPorUsuarioId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreadoPorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificadoPorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitudesPasswordReset", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesPasswordReset_UsuarioId_Estado",
                table: "SolicitudesPasswordReset",
                columns: new[] { "UsuarioId", "Estado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SolicitudesPasswordReset");
        }
    }
}
