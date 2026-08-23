using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Andalos.API.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitorPassesTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VisitorPasses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PassCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    VisitorName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    VisitorPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NationalId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    VisitorType = table.Column<int>(type: "int", nullable: false),
                    UnitId = table.Column<int>(type: "int", nullable: true),
                    ValidDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MaxEntries = table.Column<int>(type: "int", nullable: false),
                    UsedCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitorPasses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VisitorPasses_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EntryLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VisitorPassId = table.Column<int>(type: "int", nullable: false),
                    ScanTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GateName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ScannedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsAllowed = table.Column<bool>(type: "bit", nullable: false),
                    RejectReason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntryLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntryLogs_VisitorPasses_VisitorPassId",
                        column: x => x.VisitorPassId,
                        principalTable: "VisitorPasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntryLogs_VisitorPassId",
                table: "EntryLogs",
                column: "VisitorPassId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitorPasses_PassCode",
                table: "VisitorPasses",
                column: "PassCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VisitorPasses_UnitId",
                table: "VisitorPasses",
                column: "UnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EntryLogs");

            migrationBuilder.DropTable(
                name: "VisitorPasses");
        }
    }
}
