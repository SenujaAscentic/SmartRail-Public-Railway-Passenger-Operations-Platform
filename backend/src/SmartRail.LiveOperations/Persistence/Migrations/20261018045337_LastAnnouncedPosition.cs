using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRail.LiveOperations.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LastAnnouncedPosition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "FractionToNext",
                schema: "ops",
                table: "Journeys",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LastSpeedKmh",
                schema: "ops",
                table: "Journeys",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastStationId",
                schema: "ops",
                table: "Journeys",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "NextStationId",
                schema: "ops",
                table: "Journeys",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FractionToNext",
                schema: "ops",
                table: "Journeys");

            migrationBuilder.DropColumn(
                name: "LastSpeedKmh",
                schema: "ops",
                table: "Journeys");

            migrationBuilder.DropColumn(
                name: "LastStationId",
                schema: "ops",
                table: "Journeys");

            migrationBuilder.DropColumn(
                name: "NextStationId",
                schema: "ops",
                table: "Journeys");
        }
    }
}
