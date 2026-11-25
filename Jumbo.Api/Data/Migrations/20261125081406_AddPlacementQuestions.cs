using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Jumbo.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlacementQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "placement_passages",
                columns: table => new
                {
                    placement_passage_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    level = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    text = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_placement_passages", x => x.placement_passage_id);
                });

            migrationBuilder.CreateTable(
                name: "placement_questions",
                columns: table => new
                {
                    placement_question_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    skill = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    level = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    prompt = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    options = table.Column<List<string>>(type: "text[]", nullable: false),
                    answer = table.Column<int>(type: "integer", nullable: false),
                    placement_passage_id = table.Column<int>(type: "integer", nullable: true),
                    audio = table.Column<List<string>>(type: "text[]", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_placement_questions", x => x.placement_question_id);
                    table.ForeignKey(
                        name: "fk_placement_questions_placement_passages_placement_passage_id",
                        column: x => x.placement_passage_id,
                        principalTable: "placement_passages",
                        principalColumn: "placement_passage_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_placement_passages_code",
                table: "placement_passages",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_placement_questions_code",
                table: "placement_questions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_placement_questions_level_skill",
                table: "placement_questions",
                columns: new[] { "level", "skill" });

            migrationBuilder.CreateIndex(
                name: "ix_placement_questions_placement_passage_id",
                table: "placement_questions",
                column: "placement_passage_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "placement_questions");

            migrationBuilder.DropTable(
                name: "placement_passages");
        }
    }
}
