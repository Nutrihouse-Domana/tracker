using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Impressoras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Alugada",
                table: "Dispositivos",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Colorida",
                table: "Dispositivos",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Conexao",
                table: "Dispositivos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ContadorPaginas",
                table: "Dispositivos",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnderecoRede",
                table: "Dispositivos",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErroUltimaLeitura",
                table: "Dispositivos",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstadoReportado",
                table: "Dispositivos",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MonitorarSnmp",
                table: "Dispositivos",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Online",
                table: "Dispositivos",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuprimentosJson",
                table: "Dispositivos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TipoImpressora",
                table: "Dispositivos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UltimaLeituraEm",
                table: "Dispositivos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UltimaRespostaEm",
                table: "Dispositivos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LeiturasImpressora",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImpressoraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Online = table.Column<bool>(type: "bit", nullable: false),
                    ContadorPaginas = table.Column<long>(type: "bigint", nullable: true),
                    EstadoReportado = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SuprimentosJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Erro = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeiturasImpressora", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeiturasImpressora_Dispositivos_ImpressoraId",
                        column: x => x.ImpressoraId,
                        principalTable: "Dispositivos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeiturasImpressora_ImpressoraId_CriadoEm",
                table: "LeiturasImpressora",
                columns: new[] { "ImpressoraId", "CriadoEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeiturasImpressora");

            migrationBuilder.DropColumn(
                name: "Alugada",
                table: "Dispositivos");

            migrationBuilder.DropColumn(
                name: "Colorida",
                table: "Dispositivos");

            migrationBuilder.DropColumn(
                name: "Conexao",
                table: "Dispositivos");

            migrationBuilder.DropColumn(
                name: "ContadorPaginas",
                table: "Dispositivos");

            migrationBuilder.DropColumn(
                name: "EnderecoRede",
                table: "Dispositivos");

            migrationBuilder.DropColumn(
                name: "ErroUltimaLeitura",
                table: "Dispositivos");

            migrationBuilder.DropColumn(
                name: "EstadoReportado",
                table: "Dispositivos");

            migrationBuilder.DropColumn(
                name: "MonitorarSnmp",
                table: "Dispositivos");

            migrationBuilder.DropColumn(
                name: "Online",
                table: "Dispositivos");

            migrationBuilder.DropColumn(
                name: "SuprimentosJson",
                table: "Dispositivos");

            migrationBuilder.DropColumn(
                name: "TipoImpressora",
                table: "Dispositivos");

            migrationBuilder.DropColumn(
                name: "UltimaLeituraEm",
                table: "Dispositivos");

            migrationBuilder.DropColumn(
                name: "UltimaRespostaEm",
                table: "Dispositivos");
        }
    }
}
