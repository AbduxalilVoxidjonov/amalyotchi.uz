using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amaliyotchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TutorScopes : Migration
    {
        /// <summary>Har bir faol (is_active, o'chirilmagan) guruh biriktiruvi uchun guruh darajasidagi (level = 4) ko'lam;
        /// ota id'lar student_groups → directions → departments zanjiridan. Idempotent — shu tyutor+guruh uchun faol
        /// ko'lam allaqachon bo'lsa qayta yaratilmaydi. Testda ham shu matn ishlatiladi.</summary>
        public const string BackfillGroupScopesSql = """
            INSERT INTO tutor_scopes (id, tutor_user_id, level, faculty_id, department_id, direction_id, student_group_id,
                                      is_active, is_deleted, deleted_at, created_at, created_by, updated_at, updated_by)
            SELECT gen_random_uuid(), a.tutor_user_id, 4, dep.faculty_id, dep.id, dir.id, g.id,
                   true, false, NULL, a.created_at, a.created_by, NULL, NULL
            FROM tutor_assignments a
            JOIN student_groups g ON g.id = a.student_group_id
            JOIN directions dir ON dir.id = g.direction_id
            JOIN departments dep ON dep.id = dir.department_id
            WHERE a.is_active AND NOT a.is_deleted
              AND NOT EXISTS (
                  SELECT 1 FROM tutor_scopes s
                  WHERE s.tutor_user_id = a.tutor_user_id AND s.level = 4 AND s.student_group_id = a.student_group_id
                    AND NOT s.is_deleted);
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Ierarxik ko'lam jadvali: tyutor qaysi darajada (fakultet/kafedra/yo'nalish/guruh) biriktirilgani.
            // tutor_assignments (guruh darajasi) saqlanib qoladi — ko'lamlar unga materializatsiya qilinadi.
            migrationBuilder.CreateTable(
                name: "tutor_scopes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tutor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    faculty_id = table.Column<Guid>(type: "uuid", nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    direction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    student_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
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

            // 2. Ma'lumot ko'chirish: mavjud faol guruh biriktiruvlari → guruh darajasidagi ko'lamlar.
            migrationBuilder.Sql(BackfillGroupScopesSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tutor_scopes");
        }
    }
}
