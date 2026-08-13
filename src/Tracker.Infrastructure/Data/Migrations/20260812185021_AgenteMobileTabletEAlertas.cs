using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgenteMobileTabletEAlertas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BateriaPercentual",
                table: "Dispositivos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Fabricante",
                table: "Dispositivos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Modelo",
                table: "Dispositivos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NivelProtecao",
                table: "Dispositivos",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AlertasDispositivo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DispositivoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Detalhe = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reconhecido = table.Column<bool>(type: "bit", nullable: false),
                    ReconhecidoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReconhecidoPorAdmId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertasDispositivo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertasDispositivo_Dispositivos_DispositivoId",
                        column: x => x.DispositivoId,
                        principalTable: "Dispositivos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertasDispositivo_DispositivoId_Reconhecido_CriadoEm",
                table: "AlertasDispositivo",
                columns: new[] { "DispositivoId", "Reconhecido", "CriadoEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertasDispositivo");

            migrationBuilder.DropColumn(
                name: "BateriaPercentual",
                table: "Dispositivos");

            migrationBuilder.DropColumn(
                name: "Fabricante",
                table: "Dispositivos");

            migrationBuilder.DropColumn(
                name: "Modelo",
                table: "Dispositivos");

            migrationBuilder.DropColumn(
                name: "NivelProtecao",
                table: "Dispositivos");
        }
    }
}
