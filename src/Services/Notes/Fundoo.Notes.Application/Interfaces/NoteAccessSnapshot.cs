using Fundoo.Notes.Domain.Entity;

namespace Fundoo.Notes.Application.Interfaces;

public sealed record NoteAccessSnapshot(Guid OwnerId, long RowVersion, CollaboratorPermission? Permission);
