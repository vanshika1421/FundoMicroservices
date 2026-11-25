using Fundoo.Notes.Application.Interfaces;
using Fundoo.Notes.Domain.Entity;
using Microsoft.Extensions.Logging;

namespace Fundoo.Notes.Application.Services;

public sealed class NoteCollaboratorsService(
    INoteCollaboratorsRepository repository,
    ICacheService cache,
    ILogger<NoteCollaboratorsService> logger) : INoteCollaboratorsService
{
    public Task<IReadOnlyList<NoteCollaborator>> GetAllAsync(Guid noteId, Guid ownerId, CancellationToken token) =>
        repository.GetAllAsync(noteId, ownerId, token);

    public async Task<bool> AddAsync(Guid noteId, Guid ownerId, Guid userId, CollaboratorPermission permission, CancellationToken token)
    {
        if (userId == Guid.Empty) throw new ArgumentException("A valid collaborator user ID is required.", nameof(userId));
        Validate(permission);
        var changed = await repository.AddAsync(noteId, ownerId, userId, permission, token);
        if (changed) await InvalidateAsync(noteId, token);
        return changed;
    }

    public async Task<bool> RemoveAsync(Guid noteId, Guid ownerId, Guid userId, CancellationToken token)
    {
        var changed = await repository.RemoveAsync(noteId, ownerId, userId, token);
        if (changed) await InvalidateAsync(noteId, token);
        return changed;
    }

    public async Task<bool> SetPermissionAsync(Guid noteId, Guid ownerId, Guid userId, CollaboratorPermission permission, CancellationToken token)
    {
        Validate(permission);
        var changed = await repository.SetPermissionAsync(noteId, ownerId, userId, permission, token);
        if (changed) await InvalidateAsync(noteId, token);
        return changed;
    }

    private async Task InvalidateAsync(Guid noteId, CancellationToken token)
    {
        try { await cache.RemoveAsync($"note:{noteId:N}", token); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to invalidate cached note {NoteId}", noteId); }
    }

    private static void Validate(CollaboratorPermission permission)
    {
        if (!Enum.IsDefined(permission)) throw new ArgumentOutOfRangeException(nameof(permission));
    }
}
