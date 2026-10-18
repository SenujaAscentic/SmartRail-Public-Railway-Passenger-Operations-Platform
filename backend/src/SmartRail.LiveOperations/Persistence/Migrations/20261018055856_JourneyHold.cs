using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRail.LiveOperations.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class JourneyHold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HoldMinutes",
                schema: "ops",
                table: "Journeys",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HoldReasonCode",
                schema: "ops",
                table: "Journeys",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HoldReasonNote",
                schema: "ops",
                table: "Journeys",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HoldSetAt",
                schema: "ops",
                table: "Journeys",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HoldStationId",
                schema: "ops",
                table: "Journeys",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HoldMinutes",
                schema: "ops",
                table: "Journeys");

            migrationBuilder.DropColumn(
                name: "HoldReasonCode",
                schema: "ops",
                table: "Journeys");

            migrationBuilder.DropColumn(
                name: "HoldReasonNote",
                schema: "ops",
                table: "Journeys");

            migrationBuilder.DropColumn(
                name: "HoldSetAt",
                schema: "ops",
                table: "Journeys");

            migrationBuilder.DropColumn(
                name: "HoldStationId",
                schema: "ops",
                table: "Journeys");
        }
    }
}
