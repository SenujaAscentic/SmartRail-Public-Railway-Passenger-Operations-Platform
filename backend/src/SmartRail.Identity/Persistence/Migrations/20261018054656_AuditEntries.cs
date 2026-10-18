using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRail.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEntries",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    At = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TargetType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Summary = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RequestId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_TargetType_TargetId_At",
                schema: "identity",
                table: "AuditEntries",
                columns: new[] { "TargetType", "TargetId", "At" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntries",
                schema: "identity");
        }
    }
}
