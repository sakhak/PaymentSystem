using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomaticPaymentVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastCheckedAt",
                table: "Payments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextVerificationAt",
                table: "Payments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationCode",
                table: "Payments",
                type: "TEXT",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationMessage",
                table: "Payments",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Status_NextVerificationAt",
                table: "Payments",
                columns: new[] { "Status", "NextVerificationAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_Status_NextVerificationAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "LastCheckedAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "NextVerificationAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "VerificationCode",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "VerificationMessage",
                table: "Payments");
        }
    }
}
