using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Andalos.API.Migrations
{
    /// <inheritdoc />
    public partial class AddChargeApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 👈 1. نوع فوترة الصيانة (على الإدارة / عرض / إجبارية)
            migrationBuilder.AddColumn<int>(
                name: "BillingType",
                table: "MaintenanceRequests",
                type: "int",
                nullable: false,
                defaultValue: 1); // None = على الإدارة

            // 👈 2. حالة التحميل (عرض معلق / مؤكد / مرفوض / مسدد)
            migrationBuilder.AddColumn<int>(
                name: "ChargeStatus",
                table: "TenantCharges",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "RespondedAt",
                table: "TenantCharges",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "TenantCharges",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            // 👈 ترحيل البيانات القديمة: ما هو مسدد = Paid (4) وما تبقى = مؤكد Approved (2)
            migrationBuilder.Sql(@"
                UPDATE [TenantCharges]
                SET [ChargeStatus] = CASE WHEN [IsSettled] = CAST(1 AS bit) THEN 4 ELSE 2 END
                WHERE [ChargeStatus] = 1;");

            migrationBuilder.CreateIndex(
                name: "IX_TenantCharges_ChargeStatus",
                table: "TenantCharges",
                column: "ChargeStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TenantCharges_ChargeStatus",
                table: "TenantCharges");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "TenantCharges");

            migrationBuilder.DropColumn(
                name: "RespondedAt",
                table: "TenantCharges");

            migrationBuilder.DropColumn(
                name: "ChargeStatus",
                table: "TenantCharges");

            migrationBuilder.DropColumn(
                name: "BillingType",
                table: "MaintenanceRequests");
        }
    }
}
