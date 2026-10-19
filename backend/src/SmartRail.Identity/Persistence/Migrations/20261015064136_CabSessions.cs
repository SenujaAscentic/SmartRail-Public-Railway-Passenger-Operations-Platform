using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRail.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CabSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AbsoluteExpiresAt",
                schema: "identity",
                table: "RefreshTokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CabSessionId",
                schema: "identity",
                table: "RefreshTokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CabSessions",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClockOffsetMs = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CabSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CabSessions_CabDevices_DeviceId",
                        column: x => x.DeviceId,
                        principalSchema: "identity",
                        principalTable: "CabDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CabSessions_StaffAccounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "identity",
                        principalTable: "StaffAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_CabSessionId",
                schema: "identity",
                table: "RefreshTokens",
                column: "CabSessionId");

            migrationBuilder.CreateIndex(
                name: "UX_CabSessions_ActivePerDevice",
                schema: "identity",
                table: "CabSessions",
                column: "DeviceId",
                unique: true,
                filter: "\"EndedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_CabSessions_ActivePerDriver",
                schema: "identity",
                table: "CabSessions",
                column: "AccountId",
                unique: true,
                filter: "\"EndedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CabSessions",
                schema: "identity");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_CabSessionId",
                schema: "identity",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "AbsoluteExpiresAt",
                schema: "identity",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "CabSessionId",
                schema: "identity",
                table: "RefreshTokens");
        }
    }
}
