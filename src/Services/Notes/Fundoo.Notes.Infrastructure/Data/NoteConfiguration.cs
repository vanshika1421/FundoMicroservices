using Fundoo.Notes.Domain.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fundoo.Notes.Infrastructure.Data;

public sealed class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.ToTable("notes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.Title).HasColumnName("title");
        builder.Property(x => x.Content).HasColumnName("content");
        builder.Property(x => x.Color).HasColumnName("color");
        builder.Property(x => x.IsPinned).HasColumnName("is_pinned");
        builder.Property(x => x.IsArchived).HasColumnName("is_archived");
        builder.Property(x => x.IsTrashed).HasColumnName("is_trashed");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(x => x.RowVersion).HasColumnName("row_version").IsConcurrencyToken();
        builder.Ignore(x => x.NoteLabels);
        builder.HasMany(x => x.Collaborators).WithOne(x => x.Note).HasForeignKey(x => x.NoteId).OnDelete(DeleteBehavior.Cascade);
    }
}
