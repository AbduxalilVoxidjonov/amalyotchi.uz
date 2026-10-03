using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amaliyotchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StudentWorkHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "previous_work_end",
                table: "student_profiles",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "previous_work_start",
                table: "student_profiles",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "work_end",
                table: "student_profiles",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "work_hours_effective_from",
                table: "student_profiles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "work_start",
                table: "student_profiles",
                type: "time without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "previous_work_end",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "previous_work_start",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "work_end",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "work_hours_effective_from",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "work_start",
                table: "student_profiles");
        }
    }
}
