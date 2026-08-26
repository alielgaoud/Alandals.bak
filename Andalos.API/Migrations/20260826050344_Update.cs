using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Andalos.API.Migrations
{
    /// <inheritdoc />
    public partial class Update : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsChargedToTenant",
                table: "Expenses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Expenses",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_TenantId",
                table: "Expenses",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Tenants_TenantId",
                table: "Expenses",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Tenants_TenantId",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_TenantId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "IsChargedToTenant",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Expenses");
        }
    }
}
