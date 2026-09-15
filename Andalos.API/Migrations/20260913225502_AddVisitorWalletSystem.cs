using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Andalos.API.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitorWalletSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "InitialBalance",
                table: "VisitorPasses",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsPaidPass",
                table: "VisitorPasses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "IssuedByUserId",
                table: "VisitorPasses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RemainingBalance",
                table: "VisitorPasses",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "WalletStatus",
                table: "VisitorPasses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "GatekeeperShifts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalPassesIssued = table.Column<int>(type: "int", nullable: false),
                    TotalCashCollected = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsHandedOver = table.Column<bool>(type: "bit", nullable: false),
                    HandedOverAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatekeeperShifts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GatekeeperShifts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenantSettlements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SettlementDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SettlementMethod = table.Column<int>(type: "int", nullable: false),
                    ProcessedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantSettlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantSettlements_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantSettlements_Users_ProcessedByUserId",
                        column: x => x.ProcessedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PassTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VisitorPassId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: true),
                    UnitId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsSettled = table.Column<bool>(type: "bit", nullable: false),
                    SettlementId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PassTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PassTransactions_TenantSettlements_SettlementId",
                        column: x => x.SettlementId,
                        principalTable: "TenantSettlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PassTransactions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PassTransactions_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PassTransactions_VisitorPasses_VisitorPassId",
                        column: x => x.VisitorPassId,
                        principalTable: "VisitorPasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VisitorPasses_IssuedByUserId",
                table: "VisitorPasses",
                column: "IssuedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GatekeeperShifts_UserId",
                table: "GatekeeperShifts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PassTransactions_SettlementId",
                table: "PassTransactions",
                column: "SettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_PassTransactions_TenantId",
                table: "PassTransactions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PassTransactions_UnitId",
                table: "PassTransactions",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PassTransactions_VisitorPassId",
                table: "PassTransactions",
                column: "VisitorPassId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantSettlements_ProcessedByUserId",
                table: "TenantSettlements",
                column: "ProcessedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantSettlements_TenantId",
                table: "TenantSettlements",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_VisitorPasses_Users_IssuedByUserId",
                table: "VisitorPasses",
                column: "IssuedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VisitorPasses_Users_IssuedByUserId",
                table: "VisitorPasses");

            migrationBuilder.DropTable(
                name: "GatekeeperShifts");

            migrationBuilder.DropTable(
                name: "PassTransactions");

            migrationBuilder.DropTable(
                name: "TenantSettlements");

            migrationBuilder.DropIndex(
                name: "IX_VisitorPasses_IssuedByUserId",
                table: "VisitorPasses");

            migrationBuilder.DropColumn(
                name: "InitialBalance",
                table: "VisitorPasses");

            migrationBuilder.DropColumn(
                name: "IsPaidPass",
                table: "VisitorPasses");

            migrationBuilder.DropColumn(
                name: "IssuedByUserId",
                table: "VisitorPasses");

            migrationBuilder.DropColumn(
                name: "RemainingBalance",
                table: "VisitorPasses");

            migrationBuilder.DropColumn(
                name: "WalletStatus",
                table: "VisitorPasses");
        }
    }
}
