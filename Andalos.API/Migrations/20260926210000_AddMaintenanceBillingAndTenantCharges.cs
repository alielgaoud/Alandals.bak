using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Andalos.API.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceBillingAndTenantCharges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 👈 1. أعمدة تحميل المستأجر على طلبات الصيانة
            migrationBuilder.AddColumn<bool>(
                name: "BilledToTenant",
                table: "MaintenanceRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "BilledAmount",
                table: "MaintenanceRequests",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            // 👈 2. ربط المصروف بطلب الصيانة (تكلفة الصيانة المسجلة تلقائياً)
            migrationBuilder.AddColumn<int>(
                name: "MaintenanceRequestId",
                table: "Expenses",
                type: "int",
                nullable: true);

            // 👈 3. جدول متعلقات المستأجر (التحميلات المالية)
            migrationBuilder.CreateTable(
                name: "TenantCharges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChargeNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    UnitId = table.Column<int>(type: "int", nullable: true),
                    ContractId = table.Column<int>(type: "int", nullable: true),
                    MaintenanceRequestId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SettledAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ChargeDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsSettled = table.Column<bool>(type: "bit", nullable: false),
                    SettlementReceiptNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantCharges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantCharges_MaintenanceRequests_MaintenanceRequestId",
                        column: x => x.MaintenanceRequestId,
                        principalTable: "MaintenanceRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TenantCharges_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantCharges_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantCharges_ChargeNumber",
                table: "TenantCharges",
                column: "ChargeNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantCharges_IsSettled",
                table: "TenantCharges",
                column: "IsSettled");

            migrationBuilder.CreateIndex(
                name: "IX_TenantCharges_MaintenanceRequestId",
                table: "TenantCharges",
                column: "MaintenanceRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantCharges_TenantId",
                table: "TenantCharges",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantCharges_UnitId",
                table: "TenantCharges",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_MaintenanceRequestId",
                table: "Expenses",
                column: "MaintenanceRequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_MaintenanceRequests_MaintenanceRequestId",
                table: "Expenses",
                column: "MaintenanceRequestId",
                principalTable: "MaintenanceRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_MaintenanceRequests_MaintenanceRequestId",
                table: "Expenses");

            migrationBuilder.DropTable(
                name: "TenantCharges");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_MaintenanceRequestId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "MaintenanceRequestId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "BilledAmount",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "BilledToTenant",
                table: "MaintenanceRequests");
        }
    }
}
