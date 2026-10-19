using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRail.LiveOperations.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProcessedActionsAndRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "ops",
                table: "Journeys",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "ProcessedActions",
                schema: "ops",
                columns: table => new
                {
                    ClientActionId = table.Column<Guid>(type: "uuid", nullable: false),
                    JourneyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ResultJson = table.Column<string>(type: "json", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedActions", x => x.ClientActionId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedActions_CreatedAt",
                schema: "ops",
                table: "ProcessedActions",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessedActions",
                schema: "ops");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "ops",
                table: "Journeys");
        }
    }
}
