using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiftboundMetaAnalizer.Migrations
{
    /// <inheritdoc />
    public partial class AddedTorunamentResultsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TournamentResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentName = table.Column<string>(type: "text", nullable: false),
                    Standing = table.Column<int>(type: "integer", nullable: false),
                    DeckId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentResults_Decks_DeckId",
                        column: x => x.DeckId,
                        principalTable: "Decks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TournamentResults_DeckId",
                table: "TournamentResults",
                column: "DeckId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TournamentResults");
        }
    }
}
