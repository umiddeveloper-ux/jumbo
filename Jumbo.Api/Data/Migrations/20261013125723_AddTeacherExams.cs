using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Jumbo.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherExams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "teacher_exam_id",
                table: "exam_results",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "teacher_exams",
                columns: table => new
                {
                    teacher_exam_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    teacher_id = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    word_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_teacher_exams", x => x.teacher_exam_id);
                    table.ForeignKey(
                        name: "fk_teacher_exams_users_teacher_id",
                        column: x => x.teacher_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_exam_results_teacher_exam_id",
                table: "exam_results",
                column: "teacher_exam_id");

            migrationBuilder.CreateIndex(
                name: "ix_teacher_exams_code_expires_at",
                table: "teacher_exams",
                columns: new[] { "code", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_teacher_exams_teacher_id_created_at",
                table: "teacher_exams",
                columns: new[] { "teacher_id", "created_at" });

            migrationBuilder.AddForeignKey(
                name: "fk_exam_results_teacher_exams_teacher_exam_id",
                table: "exam_results",
                column: "teacher_exam_id",
                principalTable: "teacher_exams",
                principalColumn: "teacher_exam_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_exam_results_teacher_exams_teacher_exam_id",
                table: "exam_results");

            migrationBuilder.DropTable(
                name: "teacher_exams");

            migrationBuilder.DropIndex(
                name: "ix_exam_results_teacher_exam_id",
                table: "exam_results");

            migrationBuilder.DropColumn(
                name: "teacher_exam_id",
                table: "exam_results");
        }
    }
}
