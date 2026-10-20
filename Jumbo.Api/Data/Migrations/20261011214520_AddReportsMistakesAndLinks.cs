using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Jumbo.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReportsMistakesAndLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "exam_mistakes",
                columns: table => new
                {
                    exam_mistake_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    exam_result_id = table.Column<int>(type: "integer", nullable: false),
                    word = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    translation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exam_mistakes", x => x.exam_mistake_id);
                    table.ForeignKey(
                        name: "fk_exam_mistakes_exam_results_exam_result_id",
                        column: x => x.exam_result_id,
                        principalTable: "exam_results",
                        principalColumn: "exam_result_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "school_report_links",
                columns: table => new
                {
                    school_report_link_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    school_id = table.Column<int>(type: "integer", nullable: false),
                    token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_by_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_school_report_links", x => x.school_report_link_id);
                    table.ForeignKey(
                        name: "fk_school_report_links_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "school_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_exam_mistakes_exam_result_id",
                table: "exam_mistakes",
                column: "exam_result_id");

            migrationBuilder.CreateIndex(
                name: "ix_school_report_links_school_id",
                table: "school_report_links",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "ix_school_report_links_token",
                table: "school_report_links",
                column: "token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exam_mistakes");

            migrationBuilder.DropTable(
                name: "school_report_links");
        }
    }
}
