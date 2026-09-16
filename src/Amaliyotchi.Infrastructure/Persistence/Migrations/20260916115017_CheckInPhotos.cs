using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amaliyotchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CheckInPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "check_in_photo_file_id",
                table: "daily_attendances",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "check_out_photo_file_id",
                table: "daily_attendances",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "photo_file_id",
                table: "attendance_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_daily_attendances_check_in_photo_file_id",
                table: "daily_attendances",
                column: "check_in_photo_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_daily_attendances_check_out_photo_file_id",
                table: "daily_attendances",
                column: "check_out_photo_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_events_photo_file_id",
                table: "attendance_events",
                column: "photo_file_id");

            migrationBuilder.AddForeignKey(
                name: "fk_attendance_events_stored_files_photo_file_id",
                table: "attendance_events",
                column: "photo_file_id",
                principalTable: "stored_files",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_daily_attendances_stored_files_check_in_photo_file_id",
                table: "daily_attendances",
                column: "check_in_photo_file_id",
                principalTable: "stored_files",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_daily_attendances_stored_files_check_out_photo_file_id",
                table: "daily_attendances",
                column: "check_out_photo_file_id",
                principalTable: "stored_files",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_attendance_events_stored_files_photo_file_id",
                table: "attendance_events");

            migrationBuilder.DropForeignKey(
                name: "fk_daily_attendances_stored_files_check_in_photo_file_id",
                table: "daily_attendances");

            migrationBuilder.DropForeignKey(
                name: "fk_daily_attendances_stored_files_check_out_photo_file_id",
                table: "daily_attendances");

            migrationBuilder.DropIndex(
                name: "ix_daily_attendances_check_in_photo_file_id",
                table: "daily_attendances");

            migrationBuilder.DropIndex(
                name: "ix_daily_attendances_check_out_photo_file_id",
                table: "daily_attendances");

            migrationBuilder.DropIndex(
                name: "ix_attendance_events_photo_file_id",
                table: "attendance_events");

            migrationBuilder.DropColumn(
                name: "check_in_photo_file_id",
                table: "daily_attendances");

            migrationBuilder.DropColumn(
                name: "check_out_photo_file_id",
                table: "daily_attendances");

            migrationBuilder.DropColumn(
                name: "photo_file_id",
                table: "attendance_events");
        }
    }
}
