using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureBox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKeyEnvironmentAndTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EncryptionAlgorithm",
                table: "Keys",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EnvironmentTag",
                table: "Keys",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "Keys",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidFrom",
                table: "Keys",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidTo",
                table: "Keys",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EncryptionAlgorithm",
                table: "Keys");

            migrationBuilder.DropColumn(
                name: "EnvironmentTag",
                table: "Keys");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Keys");

            migrationBuilder.DropColumn(
                name: "ValidFrom",
                table: "Keys");

            migrationBuilder.DropColumn(
                name: "ValidTo",
                table: "Keys");
        }
    }
}
