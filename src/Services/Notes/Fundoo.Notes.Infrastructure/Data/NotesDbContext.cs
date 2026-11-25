using Fundoo.Notes.Domain.Entity;
using Microsoft.EntityFrameworkCore;

namespace Fundoo.Notes.Infrastructure.Data;

public sealed class NotesDbContext(DbContextOptions<NotesDbContext> options) : DbContext(options)
{
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<NoteCollaborator> NoteCollaborators => Set<NoteCollaborator>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotesDbContext).Assembly);
}
