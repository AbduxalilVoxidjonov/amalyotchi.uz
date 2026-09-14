using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Amaliyotchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "academic_years",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
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
                    table.PrimaryKey("pk_academic_years", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_role = table.Column<int>(type: "integer", nullable: true),
                    action = table.Column<int>(type: "integer", nullable: false),
                    entity_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    changes = table.Column<string>(type: "jsonb", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    trace_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "companies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tin = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    activity = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    radius_m = table.Column<int>(type: "integer", nullable: false),
                    supervisor_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    supervisor_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    mentor_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    mentor_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    location_geog = table.Column<Point>(type: "geography(Point,4326)", nullable: true, computedColumnSql: "ST_SetSRID(ST_MakePoint(longitude, latitude), 4326)::geography", stored: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_companies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "faculties",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_faculties", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "holidays",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_recurring = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_holidays", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    role = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    telegram_user_id = table.Column<long>(type: "bigint", nullable: true),
                    faculty_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "directions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    faculty_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_directions", x => x.id);
                    table.ForeignKey(
                        name: "fk_directions_faculties_faculty_id",
                        column: x => x.faculty_id,
                        principalTable: "faculties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "app_settings",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_settings", x => x.key);
                    table.ForeignKey(
                        name: "fk_app_settings_users_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "attendance_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    accuracy_m = table.Column<double>(type: "double precision", nullable: false),
                    distance_m = table.Column<double>(type: "double precision", nullable: false),
                    radius_m = table.Column<int>(type: "integer", nullable: false),
                    accepted = table.Column<bool>(type: "boolean", nullable: false),
                    reject_reason = table.Column<int>(type: "integer", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendance_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_attendance_events_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_events_users_student_user_id",
                        column: x => x.student_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "practice_periods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    academic_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    daily_start = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    daily_end = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    late_tolerance_minutes = table.Column<int>(type: "integer", nullable: false),
                    check_in_window_minutes = table.Column<int>(type: "integer", nullable: false),
                    checkout_grace_minutes = table.Column<int>(type: "integer", nullable: false),
                    work_days = table.Column<int>(type: "integer", nullable: false),
                    required_days = table.Column<int>(type: "integer", nullable: false),
                    daily_report_required = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_practice_periods", x => x.id);
                    table.ForeignKey(
                        name: "fk_practice_periods_academic_years_academic_year_id",
                        column: x => x.academic_year_id,
                        principalTable: "academic_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_practice_periods_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    replaced_by_token = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stored_files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    pages = table.Column<int>(type: "integer", nullable: true),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stored_files", x => x.id);
                    table.ForeignKey(
                        name: "fk_stored_files_users_uploaded_by_user_id",
                        column: x => x.uploaded_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "student_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    direction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    academic_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    course = table.Column<int>(type: "integer", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_student_groups", x => x.id);
                    table.ForeignKey(
                        name: "fk_student_groups_academic_years_academic_year_id",
                        column: x => x.academic_year_id,
                        principalTable: "academic_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_student_groups_directions_direction_id",
                        column: x => x.direction_id,
                        principalTable: "directions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "diary_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    text = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    learned = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: true),
                    tutor_comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_diary_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_diary_entries_practice_periods_period_id",
                        column: x => x.period_id,
                        principalTable: "practice_periods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_diary_entries_users_reviewed_by_user_id",
                        column: x => x.reviewed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_diary_entries_users_student_user_id",
                        column: x => x.student_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "practice_grades",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tutor_points = table.Column<int>(type: "integer", nullable: true),
                    reference_points = table.Column<int>(type: "integer", nullable: true),
                    conclusion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    finalized_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finalized_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_practice_grades", x => x.id);
                    table.ForeignKey(
                        name: "fk_practice_grades_practice_periods_period_id",
                        column: x => x.period_id,
                        principalTable: "practice_periods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_practice_grades_users_finalized_by_user_id",
                        column: x => x.finalized_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_practice_grades_users_student_user_id",
                        column: x => x.student_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    file_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_document_templates", x => x.id);
                    table.ForeignKey(
                        name: "fk_document_templates_stored_files_file_id",
                        column: x => x.file_id,
                        principalTable: "stored_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "leave_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date_from = table.Column<DateOnly>(type: "date", nullable: false),
                    date_to = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    attachment_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    document_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decision_comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leave_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_leave_requests_practice_periods_period_id",
                        column: x => x.period_id,
                        principalTable: "practice_periods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_leave_requests_stored_files_document_file_id",
                        column: x => x.document_file_id,
                        principalTable: "stored_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_leave_requests_users_decided_by_user_id",
                        column: x => x.decided_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_leave_requests_users_student_user_id",
                        column: x => x.student_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "practice_applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    proposed_radius_m = table.Column<int>(type: "integer", nullable: false),
                    contract_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decision_comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    checklist = table.Column<int[]>(type: "integer[]", nullable: false),
                    revision_count = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_practice_applications", x => x.id);
                    table.ForeignKey(
                        name: "fk_practice_applications_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_practice_applications_practice_periods_period_id",
                        column: x => x.period_id,
                        principalTable: "practice_periods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_practice_applications_stored_files_contract_file_id",
                        column: x => x.contract_file_id,
                        principalTable: "stored_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_practice_applications_users_decided_by_user_id",
                        column: x => x.decided_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_practice_applications_users_student_user_id",
                        column: x => x.student_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "practice_period_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_group_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_practice_period_groups", x => x.id);
                    table.ForeignKey(
                        name: "fk_practice_period_groups_practice_periods_period_id",
                        column: x => x.period_id,
                        principalTable: "practice_periods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_practice_period_groups_student_groups_student_group_id",
                        column: x => x.student_group_id,
                        principalTable: "student_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "student_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hemis_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    student_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    invite_token = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    invite_token_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_student_profiles", x => x.id);
                    table.ForeignKey(
                        name: "fk_student_profiles_student_groups_student_group_id",
                        column: x => x.student_group_id,
                        principalTable: "student_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_student_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tutor_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tutor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    academic_year_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                name: "diary_attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    diary_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stored_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_diary_attachments", x => x.id);
                    table.ForeignKey(
                        name: "fk_diary_attachments_diary_entries_diary_entry_id",
                        column: x => x.diary_entry_id,
                        principalTable: "diary_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_diary_attachments_stored_files_stored_file_id",
                        column: x => x.stored_file_id,
                        principalTable: "stored_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "daily_attendances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    check_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    check_in_distance_m = table.Column<double>(type: "double precision", nullable: true),
                    check_in_accuracy_m = table.Column<double>(type: "double precision", nullable: true),
                    check_out_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    check_out_distance_m = table.Column<double>(type: "double precision", nullable: true),
                    auto_closed = table.Column<bool>(type: "boolean", nullable: false),
                    is_suspicious = table.Column<bool>(type: "boolean", nullable: false),
                    suspicious_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_manual = table.Column<bool>(type: "boolean", nullable: false),
                    manual_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    manual_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    leave_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_daily_attendances", x => x.id);
                    table.ForeignKey(
                        name: "fk_daily_attendances_leave_requests_leave_request_id",
                        column: x => x.leave_request_id,
                        principalTable: "leave_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_daily_attendances_practice_periods_period_id",
                        column: x => x.period_id,
                        principalTable: "practice_periods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_daily_attendances_users_manual_by_user_id",
                        column: x => x.manual_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_daily_attendances_users_student_user_id",
                        column: x => x.student_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_academic_years_name",
                table: "academic_years",
                column: "name",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_app_settings_updated_by_user_id",
                table: "app_settings",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_events_company_id",
                table: "attendance_events",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_events_date_student_user_id",
                table: "attendance_events",
                columns: new[] { "date", "student_user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_events_student_user_id_received_at",
                table: "attendance_events",
                columns: new[] { "student_user_id", "received_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_entity_name_entity_id",
                table: "audit_logs",
                columns: new[] { "entity_name", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_occurred_at",
                table: "audit_logs",
                column: "occurred_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_user_id",
                table: "audit_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_companies_location_geog",
                table: "companies",
                column: "location_geog")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "ix_companies_tin",
                table: "companies",
                column: "tin",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_daily_attendances_date",
                table: "daily_attendances",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "ix_daily_attendances_leave_request_id",
                table: "daily_attendances",
                column: "leave_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_daily_attendances_manual_by_user_id",
                table: "daily_attendances",
                column: "manual_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_daily_attendances_period_id_date",
                table: "daily_attendances",
                columns: new[] { "period_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_daily_attendances_student_user_id_date",
                table: "daily_attendances",
                columns: new[] { "student_user_id", "date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_diary_attachments_diary_entry_id_stored_file_id",
                table: "diary_attachments",
                columns: new[] { "diary_entry_id", "stored_file_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_diary_attachments_stored_file_id",
                table: "diary_attachments",
                column: "stored_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_diary_entries_period_id_status",
                table: "diary_entries",
                columns: new[] { "period_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_diary_entries_reviewed_by_user_id",
                table: "diary_entries",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_diary_entries_student_user_id_date",
                table: "diary_entries",
                columns: new[] { "student_user_id", "date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_diary_entries_submitted_at",
                table: "diary_entries",
                column: "submitted_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_directions_faculty_id_code",
                table: "directions",
                columns: new[] { "faculty_id", "code" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_document_templates_file_id",
                table: "document_templates",
                column: "file_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_templates_kind_is_active",
                table: "document_templates",
                columns: new[] { "kind", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_faculties_code",
                table: "faculties",
                column: "code",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_holidays_date",
                table: "holidays",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "ix_leave_requests_decided_by_user_id",
                table: "leave_requests",
                column: "decided_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_leave_requests_document_file_id",
                table: "leave_requests",
                column: "document_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_leave_requests_period_id_status",
                table: "leave_requests",
                columns: new[] { "period_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_leave_requests_student_user_id_date_from",
                table: "leave_requests",
                columns: new[] { "student_user_id", "date_from" });

            migrationBuilder.CreateIndex(
                name: "ix_practice_applications_company_id",
                table: "practice_applications",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_practice_applications_contract_file_id",
                table: "practice_applications",
                column: "contract_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_practice_applications_decided_by_user_id",
                table: "practice_applications",
                column: "decided_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_practice_applications_period_id_status",
                table: "practice_applications",
                columns: new[] { "period_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_practice_applications_student_period_active",
                table: "practice_applications",
                columns: new[] { "student_user_id", "period_id" },
                unique: true,
                filter: "status IN (2, 3, 4)");

            migrationBuilder.CreateIndex(
                name: "ix_practice_grades_finalized_by_user_id",
                table: "practice_grades",
                column: "finalized_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_practice_grades_period_id",
                table: "practice_grades",
                column: "period_id");

            migrationBuilder.CreateIndex(
                name: "ix_practice_grades_student_user_id_period_id",
                table: "practice_grades",
                columns: new[] { "student_user_id", "period_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_practice_period_groups_period_id_student_group_id",
                table: "practice_period_groups",
                columns: new[] { "period_id", "student_group_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_practice_period_groups_student_group_id",
                table: "practice_period_groups",
                column: "student_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_practice_periods_academic_year_id",
                table: "practice_periods",
                column: "academic_year_id");

            migrationBuilder.CreateIndex(
                name: "ix_practice_periods_created_by_user_id",
                table: "practice_periods",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_practice_periods_start_date_end_date",
                table: "practice_periods",
                columns: new[] { "start_date", "end_date" });

            migrationBuilder.CreateIndex(
                name: "ix_practice_periods_status",
                table: "practice_periods",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token",
                table: "refresh_tokens",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id_expires_at",
                table: "refresh_tokens",
                columns: new[] { "user_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_stored_files_storage_path",
                table: "stored_files",
                column: "storage_path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stored_files_uploaded_by_user_id_uploaded_at",
                table: "stored_files",
                columns: new[] { "uploaded_by_user_id", "uploaded_at" });

            migrationBuilder.CreateIndex(
                name: "ix_student_groups_academic_year_id",
                table: "student_groups",
                column: "academic_year_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_groups_direction_id_academic_year_id_name",
                table: "student_groups",
                columns: new[] { "direction_id", "academic_year_id", "name" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_student_profiles_hemis_id",
                table: "student_profiles",
                column: "hemis_id",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_student_profiles_invite_token",
                table: "student_profiles",
                column: "invite_token",
                unique: true,
                filter: "invite_token IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_student_profiles_student_group_id",
                table: "student_profiles",
                column: "student_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_profiles_user_id",
                table: "student_profiles",
                column: "user_id",
                unique: true);

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
                name: "ix_users_faculty_id_role",
                table: "users",
                columns: new[] { "faculty_id", "role" });

            migrationBuilder.CreateIndex(
                name: "ix_users_phone_number",
                table: "users",
                column: "phone_number",
                unique: true,
                filter: "is_deleted = false AND phone_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_users_telegram_user_id",
                table: "users",
                column: "telegram_user_id",
                unique: true,
                filter: "telegram_user_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_settings");

            migrationBuilder.DropTable(
                name: "attendance_events");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "daily_attendances");

            migrationBuilder.DropTable(
                name: "diary_attachments");

            migrationBuilder.DropTable(
                name: "document_templates");

            migrationBuilder.DropTable(
                name: "holidays");

            migrationBuilder.DropTable(
                name: "practice_applications");

            migrationBuilder.DropTable(
                name: "practice_grades");

            migrationBuilder.DropTable(
                name: "practice_period_groups");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "student_profiles");

            migrationBuilder.DropTable(
                name: "tutor_assignments");

            migrationBuilder.DropTable(
                name: "leave_requests");

            migrationBuilder.DropTable(
                name: "diary_entries");

            migrationBuilder.DropTable(
                name: "companies");

            migrationBuilder.DropTable(
                name: "student_groups");

            migrationBuilder.DropTable(
                name: "stored_files");

            migrationBuilder.DropTable(
                name: "practice_periods");

            migrationBuilder.DropTable(
                name: "directions");

            migrationBuilder.DropTable(
                name: "academic_years");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "faculties");
        }
    }
}
