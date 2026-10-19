using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRail.Network.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StationCateringFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCateringStation",
                schema: "network",
                table: "Stations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCateringStation",
                schema: "network",
                table: "Stations");
        }
    }
}
