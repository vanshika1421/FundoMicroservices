using Fundoo.Notes.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Fundoo.Notes.Infrastructure.Database.Migrations;

[DbContext(typeof(NotesDbContext))]
[Migration("20261125000000_AddNoteCollaborators")]
public sealed class AddNoteCollaborators : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(name: "row_version", table: "notes", type: "bigint", nullable: false, defaultValue: 1L);
        migrationBuilder.CreateTable(name: "note_collaborators", columns: table => new
        {
            note_id = table.Column<Guid>(type: "uuid", nullable: false),
            user_id = table.Column<Guid>(type: "uuid", nullable: false),
            permission = table.Column<int>(type: "integer", nullable: false),
            created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("pk_note_collaborators", x => new { x.note_id, x.user_id });
            table.ForeignKey("fk_note_collaborators_notes_note_id", x => x.note_id, "notes", "id", onDelete: ReferentialAction.Cascade);
            table.CheckConstraint("ck_note_collaborators_permission", "permission IN (0, 1)");
        });
        migrationBuilder.CreateIndex(name: "ix_note_collaborators_user_id", table: "note_collaborators", column: "user_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("note_collaborators");
        migrationBuilder.DropColumn(name: "row_version", table: "notes");
    }
}
