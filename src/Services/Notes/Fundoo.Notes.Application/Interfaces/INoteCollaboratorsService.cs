using Fundoo.Notes.Domain.Entity;

namespace Fundoo.Notes.Application.Interfaces;

public interface INoteCollaboratorsService
{
    Task<IReadOnlyList<NoteCollaborator>> GetAllAsync(Guid noteId, Guid ownerId, CancellationToken token);
    Task<bool> AddAsync(Guid noteId, Guid ownerId, Guid userId, CollaboratorPermission permission, CancellationToken token);
    Task<bool> RemoveAsync(Guid noteId, Guid ownerId, Guid userId, CancellationToken token);
    Task<bool> SetPermissionAsync(Guid noteId, Guid ownerId, Guid userId, CollaboratorPermission permission, CancellationToken token);
}
