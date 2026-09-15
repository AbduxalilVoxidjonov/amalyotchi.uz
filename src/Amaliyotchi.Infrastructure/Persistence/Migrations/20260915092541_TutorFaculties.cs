using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amaliyotchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TutorFaculties : Migration
    {
        /// <summary>Har bir o'chirilmagan tyutor (users.role = 2) uchun users.faculty_id → tutor_faculties qatori.
        /// Idempotent — shu tyutor+fakultet bog'lanishi allaqachon bo'lsa qayta yaratilmaydi. Testda ham shu matn ishlatiladi.</summary>
        public const string BackfillTutorFacultiesSql = """
            INSERT INTO tutor_faculties (id, tutor_user_id, faculty_id)
            SELECT gen_random_uuid(), u.id, u.faculty_id
            FROM users u
            WHERE u.role = 2 AND u.faculty_id IS NOT NULL AND NOT u.is_deleted
              AND NOT EXISTS (
                  SELECT 1 FROM tutor_faculties tf
                  WHERE tf.tutor_user_id = u.id AND tf.faculty_id = u.faculty_id);
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Tyutor ↔ fakultet (ko'p-ko'pga) jadvali. users.faculty_id saqlanib qoladi — asosiy fakultet (auth/JWT).
            migrationBuilder.CreateTable(
                name: "tutor_faculties",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tutor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    faculty_id = table.Column<Guid>(type: "uuid", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "ix_tutor_faculties_faculty_id",
                table: "tutor_faculties",
                column: "faculty_id");

            migrationBuilder.CreateIndex(
                name: "ix_tutor_faculties_tutor_user_id_faculty_id",
                table: "tutor_faculties",
                columns: new[] { "tutor_user_id", "faculty_id" },
                unique: true);

            // 2. Ma'lumot ko'chirish: mavjud tyutorlarning yagona fakulteti → tutor_faculties.
            migrationBuilder.Sql(BackfillTutorFacultiesSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tutor_faculties");
        }
    }
}
