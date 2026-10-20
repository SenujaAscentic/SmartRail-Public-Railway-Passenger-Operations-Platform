using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRail.CateringIntegration.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeliveryFirstAttempt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FirstAttemptAt",
                schema: "catering",
                table: "Deliveries",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstAttemptAt",
                schema: "catering",
                table: "Deliveries");
        }
    }
}
