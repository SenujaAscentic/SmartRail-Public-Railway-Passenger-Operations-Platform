using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRail.LiveOperations.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DelaySetAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DelaySetAt",
                schema: "ops",
                table: "Journeys",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DelaySetAt",
                schema: "ops",
                table: "Journeys");
        }
    }
}
