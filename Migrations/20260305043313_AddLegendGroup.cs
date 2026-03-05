using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiftboundMetaAnalizer.Migrations
{
    /// <inheritdoc />
    public partial class AddLegendGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LegendGroup",
                table: "Cards",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LegendGroup",
                table: "Cards");
        }
    }
}
