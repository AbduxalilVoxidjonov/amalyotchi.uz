using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amaliyotchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FaceVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "face_match_score",
                table: "daily_attendances",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "face_match_score",
                table: "attendance_events",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "student_face_enrollments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    photo_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    embedding = table.Column<float[]>(type: "real[]", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reject_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_student_face_enrollments", x => x.id);
                    table.ForeignKey(
                        name: "fk_student_face_enrollments_stored_files_photo_file_id",
                        column: x => x.photo_file_id,
                        principalTable: "stored_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_student_face_enrollments_users_reviewed_by_user_id",
                        column: x => x.reviewed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_student_face_enrollments_users_student_user_id",
                        column: x => x.student_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_student_face_enrollments_photo_file_id",
                table: "student_face_enrollments",
                column: "photo_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_face_enrollments_reviewed_by_user_id",
                table: "student_face_enrollments",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_face_enrollments_status_submitted_at",
                table: "student_face_enrollments",
                columns: new[] { "status", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_student_face_enrollments_student_user_id",
                table: "student_face_enrollments",
                column: "student_user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "student_face_enrollments");

            migrationBuilder.DropColumn(
                name: "face_match_score",
                table: "daily_attendances");

            migrationBuilder.DropColumn(
                name: "face_match_score",
                table: "attendance_events");
        }
    }
}
