using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tracker.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Unifica Telefone e Tablet num único tipo Mobile.
    ///
    /// O EF não gera nada automaticamente aqui: a mudança é de DADOS, não de schema. O
    /// discriminador TPH é um número na coluna Tipo — Telefone valia 1 e virou Mobile mantendo o
    /// mesmo 1 (nada a fazer nesses registros), enquanto Tablet valia 3, valor que deixou de
    /// existir. Sem este UPDATE, os tablets já cadastrados ficariam com um discriminador órfão e
    /// o EF lançaria erro ao tentar materializá-los.
    /// </summary>
    public partial class UnificaTelefoneETabletEmMobile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 3 = antigo TipoDispositivo.Tablet -> 1 = TipoDispositivo.Mobile.
            // O IMEI já morava na coluna Imei nos dois tipos, então não há dado a mover.
            migrationBuilder.Sql("UPDATE Dispositivos SET Tipo = 1 WHERE Tipo = 3;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sem volta possível: depois da unificação não há como saber quais Mobiles eram
            // tablets e quais eram celulares. Reverter deixaria todos como celular, o que seria
            // uma perda silenciosa de informação — melhor falhar e deixar a decisão para a TI.
            throw new System.InvalidOperationException(
                "Esta migração não pode ser revertida: a distinção entre telefone e tablet foi " +
                "descartada de propósito. Restaure um backup se precisar do estado anterior.");
        }
    }
}
