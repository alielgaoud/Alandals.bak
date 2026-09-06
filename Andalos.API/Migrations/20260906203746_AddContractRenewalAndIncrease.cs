using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Andalos.API.Migrations
{
    /// <inheritdoc />
    public partial class AddContractRenewalAndIncrease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AnnualIncreasePercentage",
                table: "Contracts",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentContractId",
                table: "Contracts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ParentContractId",
                table: "Contracts",
                column: "ParentContractId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_Contracts_ParentContractId",
                table: "Contracts",
                column: "ParentContractId",
                principalTable: "Contracts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_Contracts_ParentContractId",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_ParentContractId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "AnnualIncreasePercentage",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ParentContractId",
                table: "Contracts");
        }
    }
}
