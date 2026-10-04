using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amaliyotchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTutors : Migration
    {
        /// <summary>Faol bosh admin yo'q bo'lsa — eng eski faol, o'chirilmagan admin bosh admin qilinadi (idempotent).</summary>
        public const string PromoteOldestAdminSql = """
                UPDATE users SET is_super_admin = true
                WHERE id = (
                        SELECT id FROM users
                        WHERE role = 1 AND is_active AND NOT is_deleted
                        ORDER BY created_at, id
                        LIMIT 1)
                  AND NOT EXISTS (
                        SELECT 1 FROM users
                        WHERE role = 1 AND is_active AND NOT is_deleted AND is_super_admin);
            """;

        /// <summary>Rol 2 (tyutor) foydalanuvchilarning bekor qilinmagan refresh tokenlari bekor qilinadi (idempotent).</summary>
        public const string RevokeTutorRefreshTokensSql = """
                UPDATE refresh_tokens
                SET revoked_at = now(), revoked_reason = 'Tyutor roli olib tashlandi'
                WHERE revoked_at IS NULL
                  AND user_id IN (SELECT id FROM users WHERE role = 2);
            """;

        /// <summary>Rol 2 (tyutor) foydalanuvchilar soft-delete qilinadi va faolsizlantiriladi (idempotent; <c>deleted_at</c> saqlanadi).</summary>
        public const string SoftDeleteTutorsSql = """
                UPDATE users
                SET is_deleted = true, deleted_at = COALESCE(deleted_at, now()), is_active = false
                WHERE role = 2 AND (NOT is_deleted OR is_active);
            """;

        /// <inheritdoc />
        /// <remarks>Tyutor roli olib tashlanadi — barcha vazifalar adminga o'tadi. Production ma'lumotiga ta'siri (SQL qayta
        /// ishga tushirilsa ham xavfsiz — har qadam o'z shartiga ega):
        /// <list type="number">
        /// <item><c>users.is_super_admin</c> qo'shiladi; faol bosh admin hali yo'q bo'lsa — eng eski (<c>created_at</c>, keyin
        /// <c>id</c>) faol, o'chirilmagan admin bosh admin qilinadi (adminsiz bazada hech narsa qilinmaydi — DbSeeder
        /// birinchi adminni bosh admin sifatida yaratadi).</item>
        /// <item>Rol <c>2</c> (tyutor) bo'lgan barcha foydalanuvchilarning bekor qilinmagan refresh tokenlari bekor qilinadi.</item>
        /// <item>O'sha foydalanuvchilar soft-delete qilinadi (<c>is_deleted=true</c>, <c>deleted_at=now()</c>, <c>is_active=false</c>).
        /// Qatorlar o'chirilmaydi — audit, ariza/kundalik qarorlari (<c>decided_by_user_id</c> va h.k.) tarixi saqlanadi;
        /// HEMIS ID (login) qayta ishlatilmaydi, telefon esa bo'shaydi (unikal indeks <c>is_deleted=false</c> filtrli).</item>
        /// <item>Tyutor ko'lami jadvallari (<c>tutor_assignments</c>, <c>tutor_scopes</c>, <c>tutor_faculties</c>) o'chiriladi.</item>
        /// </list></remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_super_admin",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(PromoteOldestAdminSql);

            migrationBuilder.Sql(RevokeTutorRefreshTokensSql);

            migrationBuilder.Sql(SoftDeleteTutorsSql);

            migrationBuilder.DropTable(
                name: "tutor_assignments");

            migrationBuilder.DropTable(
                name: "tutor_faculties");

            migrationBuilder.DropTable(
                name: "tutor_scopes");
        }

        /// <summary>Faqat sxema qaytariladi: tyutor ko'lami jadvallari bo'sh yaratiladi; soft-delete qilingan tyutorlar va
        /// bosh admin belgisi tiklanmaydi (ma'lumot qaytarilmaydi).</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_super_admin",
                table: "users");

            migrationBuilder.CreateTable(
                name: "tutor_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    academic_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    tutor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tutor_assignments", x => x.id);
                    table.ForeignKey(
                        name: "fk_tutor_assignments_academic_years_academic_year_id",
                        column: x => x.academic_year_id,
                        principalTable: "academic_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tutor_assignments_student_groups_student_group_id",
                        column: x => x.student_group_id,
                        principalTable: "student_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tutor_assignments_users_tutor_user_id",
                        column: x => x.tutor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tutor_faculties",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    faculty_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tutor_user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tutor_faculties", x => x.id);
                    table.ForeignKey(
                        name: "fk_tutor_faculties_faculties_faculty_id",
                        column: x => x.faculty_id,
                        principalTable: "faculties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tutor_faculties_users_tutor_user_id",
                        column: x => x.tutor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tutor_scopes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    direction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    faculty_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    student_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tutor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tutor_scopes", x => x.id);
                    table.ForeignKey(
                        name: "fk_tutor_scopes_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tutor_scopes_directions_direction_id",
                        column: x => x.direction_id,
                        principalTable: "directions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tutor_scopes_faculties_faculty_id",
                        column: x => x.faculty_id,
                        principalTable: "faculties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tutor_scopes_student_groups_student_group_id",
                        column: x => x.student_group_id,
                        principalTable: "student_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tutor_scopes_users_tutor_user_id",
                        column: x => x.tutor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tutor_assignments_academic_year_id",
                table: "tutor_assignments",
                column: "academic_year_id");

            migrationBuilder.CreateIndex(
                name: "ix_tutor_assignments_student_group_id",
                table: "tutor_assignments",
                column: "student_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_tutor_assignments_tutor_user_id_student_group_id",
                table: "tutor_assignments",
                columns: new[] { "tutor_user_id", "student_group_id" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_tutor_faculties_faculty_id",
                table: "tutor_faculties",
                column: "faculty_id");

            migrationBuilder.CreateIndex(
                name: "ix_tutor_faculties_tutor_user_id_faculty_id",
                table: "tutor_faculties",
                columns: new[] { "tutor_user_id", "faculty_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tutor_scopes_department_id",
                table: "tutor_scopes",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_tutor_scopes_direction_id",
                table: "tutor_scopes",
                column: "direction_id");

            migrationBuilder.CreateIndex(
                name: "ix_tutor_scopes_faculty_id",
                table: "tutor_scopes",
                column: "faculty_id",
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_tutor_scopes_student_group_id",
                table: "tutor_scopes",
                column: "student_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_tutor_scopes_tutor_user_id",
                table: "tutor_scopes",
                column: "tutor_user_id");
        }
    }
}
