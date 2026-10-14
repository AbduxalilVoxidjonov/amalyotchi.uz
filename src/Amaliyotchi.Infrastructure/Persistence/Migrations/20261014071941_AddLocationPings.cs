using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amaliyotchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationPings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "tracking_consent_at",
                table: "student_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "location_pings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attendance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    accuracy_m = table.Column<double>(type: "double precision", nullable: false),
                    distance_m = table.Column<double>(type: "double precision", nullable: false),
                    radius_m = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    is_mocked = table.Column<bool>(type: "boolean", nullable: false),
                    source = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_location_pings", x => x.id);
                    table.ForeignKey(
                        name: "fk_location_pings_daily_attendances_attendance_id",
                        column: x => x.attendance_id,
                        principalTable: "daily_attendances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_location_pings_users_student_user_id",
                        column: x => x.student_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_location_pings_attendance_id",
                table: "location_pings",
                column: "attendance_id");

            migrationBuilder.CreateIndex(
                name: "ix_location_pings_date_student_user_id",
                table: "location_pings",
                columns: new[] { "date", "student_user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_location_pings_student_user_id_occurred_at",
                table: "location_pings",
                columns: new[] { "student_user_id", "occurred_at" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "location_pings");

            migrationBuilder.DropColumn(
                name: "tracking_consent_at",
                table: "student_profiles");
        }
    }
}
