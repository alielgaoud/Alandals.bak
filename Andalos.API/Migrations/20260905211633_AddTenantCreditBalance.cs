using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Andalos.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantCreditBalance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CreditBalance",
                table: "Tenants",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "NumberSequences",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "NumberSequences",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "NumberSequences",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LastYear",
                table: "NumberSequences",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "NumberSequences",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreditBalance",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "NumberSequences");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "NumberSequences");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "NumberSequences");

            migrationBuilder.DropColumn(
                name: "LastYear",
                table: "NumberSequences");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "NumberSequences");
        }
    }
}
