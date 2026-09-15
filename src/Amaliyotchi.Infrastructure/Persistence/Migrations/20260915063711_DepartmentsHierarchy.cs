using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amaliyotchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DepartmentsHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Yangi "kafedra" darajasi — fakultet va yo'nalish orasida.
            migrationBuilder.CreateTable(
                name: "departments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    faculty_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_departments", x => x.id);
                    table.ForeignKey(
                        name: "fk_departments_faculties_faculty_id",
                        column: x => x.faculty_id,
                        principalTable: "faculties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_departments_faculty_id_code",
                table: "departments",
                columns: new[] { "faculty_id", "code" },
                unique: true,
                filter: "is_deleted = false");

            // 2. Ma'lumot ko'chirish: har fakultet uchun "Umumiy kafedra" (kod = fakultet kodi) — idempotent
            // (qayta ishga tushirilsa allaqachon mavjud kafedralar qayta yaratilmaydi).
            migrationBuilder.Sql("""
                INSERT INTO departments (id, faculty_id, name, code, is_active, is_deleted, deleted_at, created_at, updated_at, created_by, updated_by)
                SELECT gen_random_uuid(), f.id, 'Umumiy kafedra', f.code, true, false, NULL, now(), NULL, NULL, NULL
                FROM faculties f
                WHERE NOT EXISTS (SELECT 1 FROM departments d WHERE d.faculty_id = f.id AND d.code = f.code);
                """);

            // 3. directions.department_id — avval nullable qo'shiladi, mavjud yo'nalishlar yuqoridagi
            // kafedraga bog'lanadi, keyin NOT NULL qilinadi (prod-safe, ikki bosqichli).
            migrationBuilder.AddColumn<Guid>(
                name: "department_id",
                table: "directions",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE directions AS dir
                SET department_id = dep.id
                FROM departments AS dep
                WHERE dep.faculty_id = dir.faculty_id
                  AND dir.department_id IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "department_id",
                table: "directions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            // 4. Eski faculty_id ustuni, uning indeksi va FK'si olib tashlanadi.
            migrationBuilder.DropForeignKey(
                name: "fk_directions_faculties_faculty_id",
                table: "directions");

            migrationBuilder.DropIndex(
                name: "ix_directions_faculty_id_code",
                table: "directions");

            migrationBuilder.DropColumn(
                name: "faculty_id",
                table: "directions");

            migrationBuilder.CreateIndex(
                name: "ix_directions_department_id_code",
                table: "directions",
                columns: new[] { "department_id", "code" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.AddForeignKey(
                name: "fk_directions_departments_department_id",
                table: "directions",
                column: "department_id",
                principalTable: "departments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // 5. Yo'nalish va guruh uchun faol/faol emas bayrog'i — mavjud yozuvlar faol deb hisoblanadi.
            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "directions",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "student_groups",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_active",
                table: "student_groups");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "directions");

            migrationBuilder.DropForeignKey(
                name: "fk_directions_departments_department_id",
                table: "directions");

            migrationBuilder.DropIndex(
                name: "ix_directions_department_id_code",
                table: "directions");

            migrationBuilder.AddColumn<Guid>(
                name: "faculty_id",
                table: "directions",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE directions AS dir
                SET faculty_id = dep.faculty_id
                FROM departments AS dep
                WHERE dep.id = dir.department_id
                  AND dir.faculty_id IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "faculty_id",
                table: "directions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "department_id",
                table: "directions");

            migrationBuilder.CreateIndex(
                name: "ix_directions_faculty_id_code",
                table: "directions",
                columns: new[] { "faculty_id", "code" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.AddForeignKey(
                name: "fk_directions_faculties_faculty_id",
                table: "directions",
                column: "faculty_id",
                principalTable: "faculties",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropTable(
                name: "departments");
        }
    }
}
