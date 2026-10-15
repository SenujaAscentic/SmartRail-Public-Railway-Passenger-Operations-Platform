using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRail.LiveOperations.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LiveJourneyState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ActualArrival",
                schema: "ops",
                table: "JourneyStops",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActualArrivalSource",
                schema: "ops",
                table: "JourneyStops",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActualDeparture",
                schema: "ops",
                table: "JourneyStops",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActualDepartureSource",
                schema: "ops",
                table: "JourneyStops",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlowSince",
                schema: "ops",
                table: "JourneyStops",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "ops",
                table: "JourneyStops",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Zone",
                schema: "ops",
                table: "JourneyStops",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DelayMinutes",
                schema: "ops",
                table: "Journeys",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DelaySource",
                schema: "ops",
                table: "Journeys",
                type: "integer",
                nullable: false,
                defaultValue: 4);   // Source.Schedule: journeys that exist already have no delay source yet

            migrationBuilder.AddColumn<double>(
                name: "LastLatitude",
                schema: "ops",
                table: "Journeys",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LastLongitude",
                schema: "ops",
                table: "Journeys",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPositionAt",
                schema: "ops",
                table: "Journeys",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Tracking",
                schema: "ops",
                table: "Journeys",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActualArrival",
                schema: "ops",
                table: "JourneyStops");

            migrationBuilder.DropColumn(
                name: "ActualArrivalSource",
                schema: "ops",
                table: "JourneyStops");

            migrationBuilder.DropColumn(
                name: "ActualDeparture",
                schema: "ops",
                table: "JourneyStops");

            migrationBuilder.DropColumn(
                name: "ActualDepartureSource",
                schema: "ops",
                table: "JourneyStops");

            migrationBuilder.DropColumn(
                name: "SlowSince",
                schema: "ops",
                table: "JourneyStops");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "ops",
                table: "JourneyStops");

            migrationBuilder.DropColumn(
                name: "Zone",
                schema: "ops",
                table: "JourneyStops");

            migrationBuilder.DropColumn(
                name: "DelayMinutes",
                schema: "ops",
                table: "Journeys");

            migrationBuilder.DropColumn(
                name: "DelaySource",
                schema: "ops",
                table: "Journeys");

            migrationBuilder.DropColumn(
                name: "LastLatitude",
                schema: "ops",
                table: "Journeys");

            migrationBuilder.DropColumn(
                name: "LastLongitude",
                schema: "ops",
                table: "Journeys");

            migrationBuilder.DropColumn(
                name: "LastPositionAt",
                schema: "ops",
                table: "Journeys");

            migrationBuilder.DropColumn(
                name: "Tracking",
                schema: "ops",
                table: "Journeys");
        }
    }
}
