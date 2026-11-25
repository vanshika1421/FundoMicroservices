using Fundoo.Notes.Domain.Entity;

namespace Fundoo.Notes.Application.Interfaces;

public interface INoteCollaboratorsRepository
{
    Task<Guid?> GetOwnerIdAsync(Guid noteId, CancellationToken cancellationToken = default);
    Task<CollaboratorPermission?> GetPermissionAsync(Guid noteId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NoteCollaborator>> GetAllAsync(Guid noteId, Guid ownerId, CancellationToken cancellationToken = default);
    Task<bool> AddAsync(Guid noteId, Guid ownerId, Guid userId, CollaboratorPermission permission, CancellationToken cancellationToken = default);
    Task<bool> RemoveAsync(Guid noteId, Guid ownerId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> SetPermissionAsync(Guid noteId, Guid ownerId, Guid userId, CollaboratorPermission permission, CancellationToken cancellationToken = default);
}
