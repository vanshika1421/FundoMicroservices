using Fundoo.Notes.Domain.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fundoo.Notes.Infrastructure.Data;

public sealed class NoteCollaboratorConfiguration : IEntityTypeConfiguration<NoteCollaborator>
{
    public void Configure(EntityTypeBuilder<NoteCollaborator> builder)
    {
        builder.ToTable("note_collaborators");
        builder.Property(x => x.NoteId).HasColumnName("note_id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.HasKey(x => new { x.NoteId, x.UserId });
        builder.Property(x => x.Permission).HasColumnName("permission").HasConversion<int>().IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.HasIndex(x => x.UserId);
        builder.HasOne(x => x.Note).WithMany(x => x.Collaborators)
            .HasForeignKey(x => x.NoteId).OnDelete(DeleteBehavior.Cascade);
    }
}
