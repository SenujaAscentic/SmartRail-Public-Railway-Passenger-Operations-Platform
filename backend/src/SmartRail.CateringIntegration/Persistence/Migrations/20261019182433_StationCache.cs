using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRail.CateringIntegration.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StationCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CachedStations",
                schema: "catering",
                columns: table => new
                {
                    StationRef = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Hotline = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Opens = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Closes = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    ProviderAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    MenuJson = table.Column<string>(type: "text", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CachedStations", x => x.StationRef);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CachedStations",
                schema: "catering");
        }
    }
}
