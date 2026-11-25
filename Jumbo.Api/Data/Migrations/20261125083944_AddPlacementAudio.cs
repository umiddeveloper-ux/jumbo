using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jumbo.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlacementAudio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "placement_audios",
                columns: table => new
                {
                    placement_question_id = table.Column<int>(type: "integer", nullable: false),
                    content_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    data = table.Column<byte[]>(type: "bytea", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_placement_audios", x => x.placement_question_id);
                    table.ForeignKey(
                        name: "fk_placement_audios_placement_questions_placement_question_id",
                        column: x => x.placement_question_id,
                        principalTable: "placement_questions",
                        principalColumn: "placement_question_id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "placement_audios");
        }
    }
}
