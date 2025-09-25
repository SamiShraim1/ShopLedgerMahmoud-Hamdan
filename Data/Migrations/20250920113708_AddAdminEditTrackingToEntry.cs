using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopLedger.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminEditTrackingToEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAdminEdited",
                table: "Entries",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedAtUtc",
                table: "Entries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedByUserId",
                table: "Entries",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAdminEdited",
                table: "Entries");

            migrationBuilder.DropColumn(
                name: "LastModifiedAtUtc",
                table: "Entries");

            migrationBuilder.DropColumn(
                name: "LastModifiedByUserId",
                table: "Entries");
        }
    }
}
