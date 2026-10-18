using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRail.LiveOperations.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ControllerDecisionColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastDecisionAt",
                schema: "ops",
                table: "Journeys",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastDecisionBy",
                schema: "ops",
                table: "Journeys",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReasonCode",
                schema: "ops",
                table: "Journeys",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReasonNote",
                schema: "ops",
                table: "Journeys",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastDecisionAt",
                schema: "ops",
                table: "Journeys");

            migrationBuilder.DropColumn(
                name: "LastDecisionBy",
                schema: "ops",
                table: "Journeys");

            migrationBuilder.DropColumn(
                name: "ReasonCode",
                schema: "ops",
                table: "Journeys");

            migrationBuilder.DropColumn(
                name: "ReasonNote",
                schema: "ops",
                table: "Journeys");
        }
    }
}
