using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UnidadeEPosseComExclusaoLogica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExcluidoEm",
                table: "Dispositivos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExcluidoPorAdmId",
                table: "Dispositivos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoExclusao",
                table: "Dispositivos",
                type: "nvarchar(max)",
                nullable: true);

            // defaultValue TRUE de propósito: o EF geraria false para bit, e isso reclassificaria
            // TODOS os dispositivos já cadastrados como "ativo do local" — sem unidade definida,
            // fazendo o detentor deles desaparecer das telas. O acervo existente segue o fluxo
            // normal (pertence a um usuário); só o que a TI marcar depois passa a ser do local.
            migrationBuilder.AddColumn<bool>(
                name: "PertenceAUsuario",
                table: "Dispositivos",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UnidadeId",
                table: "Dispositivos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Unidades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Ativa = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Unidades", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Dispositivos_ExcluidoEm",
                table: "Dispositivos",
                column: "ExcluidoEm");

            migrationBuilder.CreateIndex(
                name: "IX_Dispositivos_UnidadeId",
                table: "Dispositivos",
                column: "UnidadeId");

            migrationBuilder.CreateIndex(
                name: "IX_Unidades_Nome",
                table: "Unidades",
                column: "Nome",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Dispositivos_Unidades_UnidadeId",
                table: "Dispositivos",
                column: "UnidadeId",
                principalTable: "Unidades",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Dispositivos_Unidades_UnidadeId",
                table: "Dispositivos");

            migrationBuilder.DropTable(
                name: "Unidades");

            migrationBuilder.DropIndex(
                name: "IX_Dispositivos_ExcluidoEm",
                table: "Dispositivos");

            migrationBuilder.DropIndex(
                name: "IX_Dispositivos_UnidadeId",
                table: "Dispositivos");

            migrationBuilder.DropColumn(
                name: "ExcluidoEm",
                table: "Dispositivos");

            migrationBuilder.DropColumn(
                name: "ExcluidoPorAdmId",
                table: "Dispositivos");

            migrationBuilder.DropColumn(
                name: "MotivoExclusao",
                table: "Dispositivos");

            migrationBuilder.DropColumn(
                name: "PertenceAUsuario",
                table: "Dispositivos");

            migrationBuilder.DropColumn(
                name: "UnidadeId",
                table: "Dispositivos");
        }
    }
}
