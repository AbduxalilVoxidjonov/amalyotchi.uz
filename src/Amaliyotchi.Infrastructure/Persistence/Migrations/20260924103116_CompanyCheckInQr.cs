using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amaliyotchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompanyCheckInQr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Ustunlar avval NULL ruxsat bilan — mavjud korxonalar uchun qiymat keyin to'ldiriladi.
            migrationBuilder.AddColumn<string>(
                name: "check_in_qr_token",
                table: "companies",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "check_in_qr_rotated_at",
                table: "companies",
                type: "timestamp with time zone",
                nullable: true);

            // 2) Backfill: har korxonaga alohida 32 belgili hex token (uuid'dan chiziqchalarsiz — 122 bit tasodif).
            // Keyingi almashtirishlar ilovada RandomNumberGenerator (128 bit) bilan.
            migrationBuilder.Sql(
                """
                UPDATE companies
                SET check_in_qr_token = replace(gen_random_uuid()::text, '-', ''),
                    check_in_qr_rotated_at = now()
                WHERE check_in_qr_token IS NULL;
                """);

            // 3) Endi majburiy.
            migrationBuilder.AlterColumn<string>(
                name: "check_in_qr_token",
                table: "companies",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "check_in_qr_rotated_at",
                table: "companies",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_companies_check_in_qr_token",
                table: "companies",
                column: "check_in_qr_token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_companies_check_in_qr_token",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "check_in_qr_rotated_at",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "check_in_qr_token",
                table: "companies");
        }
    }
}
