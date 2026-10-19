using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRail.LiveOperations.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StopPlatforms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Platform",
                schema: "ops",
                table: "JourneyStops",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PreviousPlatform",
                schema: "ops",
                table: "JourneyStops",
                type: "text",
                nullable: true);

            // Journeys that already exist are on the planned platform. (Hand-written: EF cannot know this.)
            migrationBuilder.Sql("UPDATE ops.\"JourneyStops\" SET \"Platform\" = \"PlannedPlatform\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Platform",
                schema: "ops",
                table: "JourneyStops");

            migrationBuilder.DropColumn(
                name: "PreviousPlatform",
                schema: "ops",
                table: "JourneyStops");
        }
    }
}
