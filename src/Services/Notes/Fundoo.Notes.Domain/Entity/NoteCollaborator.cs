namespace Fundoo.Notes.Domain.Entity;

public sealed class NoteCollaborator
{
    public Guid NoteId { get; set; }
    public Guid UserId { get; set; }
    public CollaboratorPermission Permission { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Note Note { get; set; } = null!;
}

public enum CollaboratorPermission
{
    Read = 0,
    Edit = 1
}
