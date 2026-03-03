using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RiftboundMetaAnalizer.Migrations
{
    /// <inheritdoc />
    public partial class AddedTorunamentTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Date",
                table: "TournamentResults");

            migrationBuilder.DropColumn(
                name: "TournamentName",
                table: "TournamentResults");

            migrationBuilder.AddColumn<int>(
                name: "TournamentId",
                table: "TournamentResults",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Tournaments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tournaments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TournamentResults_TournamentId",
                table: "TournamentResults",
                column: "TournamentId");

            migrationBuilder.AddForeignKey(
                name: "FK_TournamentResults_Tournaments_TournamentId",
                table: "TournamentResults",
                column: "TournamentId",
                principalTable: "Tournaments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TournamentResults_Tournaments_TournamentId",
                table: "TournamentResults");

            migrationBuilder.DropTable(
                name: "Tournaments");

            migrationBuilder.DropIndex(
                name: "IX_TournamentResults_TournamentId",
                table: "TournamentResults");

            migrationBuilder.DropColumn(
                name: "TournamentId",
                table: "TournamentResults");

            migrationBuilder.AddColumn<DateTime>(
                name: "Date",
                table: "TournamentResults",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "TournamentName",
                table: "TournamentResults",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
