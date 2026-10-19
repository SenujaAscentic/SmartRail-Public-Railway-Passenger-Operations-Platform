using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRail.Network.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialNetwork : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "network");

            migrationBuilder.CreateTable(
                name: "Calendars",
                schema: "network",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DaysOfWeek = table.Column<int[]>(type: "integer[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Calendars", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Lines",
                schema: "network",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stations",
                schema: "network",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    ZoneRadiusM = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrainSets",
                schema: "network",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Traction = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainSets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CalendarExceptions",
                schema: "network",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CalendarId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Runs = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarExceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarExceptions_Calendars_CalendarId",
                        column: x => x.CalendarId,
                        principalSchema: "network",
                        principalTable: "Calendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Routes",
                schema: "network",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LineId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Routes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Routes_Lines_LineId",
                        column: x => x.LineId,
                        principalSchema: "network",
                        principalTable: "Lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Platforms",
                schema: "network",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    InService = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Platforms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Platforms_Stations_StationId",
                        column: x => x.StationId,
                        principalSchema: "network",
                        principalTable: "Stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Coaches",
                schema: "network",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainSetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Class = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Seating = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Coaches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Coaches_TrainSets_TrainSetId",
                        column: x => x.TrainSetId,
                        principalSchema: "network",
                        principalTable: "TrainSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RouteStations",
                schema: "network",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    StationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DistanceKm = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteStations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RouteStations_Routes_RouteId",
                        column: x => x.RouteId,
                        principalSchema: "network",
                        principalTable: "Routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RouteStations_Stations_StationId",
                        column: x => x.StationId,
                        principalSchema: "network",
                        principalTable: "Stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Services",
                schema: "network",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CalendarId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Services_Calendars_CalendarId",
                        column: x => x.CalendarId,
                        principalSchema: "network",
                        principalTable: "Calendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Services_Routes_RouteId",
                        column: x => x.RouteId,
                        principalSchema: "network",
                        principalTable: "Routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledStops",
                schema: "network",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    StationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CallType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ArrivalOffset = table.Column<int>(type: "integer", nullable: true),
                    DepartureOffset = table.Column<int>(type: "integer", nullable: true),
                    PlannedPlatform = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledStops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledStops_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalSchema: "network",
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduledStops_Stations_StationId",
                        column: x => x.StationId,
                        principalSchema: "network",
                        principalTable: "Stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarExceptions_CalendarId_Date",
                schema: "network",
                table: "CalendarExceptions",
                columns: new[] { "CalendarId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Calendars_Code",
                schema: "network",
                table: "Calendars",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Coaches_TrainSetId_Position",
                schema: "network",
                table: "Coaches",
                columns: new[] { "TrainSetId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lines_Code",
                schema: "network",
                table: "Lines",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Platforms_StationId_Number",
                schema: "network",
                table: "Platforms",
                columns: new[] { "StationId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Routes_Code",
                schema: "network",
                table: "Routes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Routes_LineId",
                schema: "network",
                table: "Routes",
                column: "LineId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteStations_RouteId_Sequence",
                schema: "network",
                table: "RouteStations",
                columns: new[] { "RouteId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RouteStations_RouteId_StationId",
                schema: "network",
                table: "RouteStations",
                columns: new[] { "RouteId", "StationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RouteStations_StationId",
                schema: "network",
                table: "RouteStations",
                column: "StationId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledStops_ServiceId_Sequence",
                schema: "network",
                table: "ScheduledStops",
                columns: new[] { "ServiceId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledStops_StationId",
                schema: "network",
                table: "ScheduledStops",
                column: "StationId");

            migrationBuilder.CreateIndex(
                name: "IX_Services_CalendarId",
                schema: "network",
                table: "Services",
                column: "CalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_Services_Number",
                schema: "network",
                table: "Services",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Services_RouteId",
                schema: "network",
                table: "Services",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_Stations_Code",
                schema: "network",
                table: "Stations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrainSets_Code",
                schema: "network",
                table: "TrainSets",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarExceptions",
                schema: "network");

            migrationBuilder.DropTable(
                name: "Coaches",
                schema: "network");

            migrationBuilder.DropTable(
                name: "Platforms",
                schema: "network");

            migrationBuilder.DropTable(
                name: "RouteStations",
                schema: "network");

            migrationBuilder.DropTable(
                name: "ScheduledStops",
                schema: "network");

            migrationBuilder.DropTable(
                name: "TrainSets",
                schema: "network");

            migrationBuilder.DropTable(
                name: "Services",
                schema: "network");

            migrationBuilder.DropTable(
                name: "Stations",
                schema: "network");

            migrationBuilder.DropTable(
                name: "Calendars",
                schema: "network");

            migrationBuilder.DropTable(
                name: "Routes",
                schema: "network");

            migrationBuilder.DropTable(
                name: "Lines",
                schema: "network");
        }
    }
}
