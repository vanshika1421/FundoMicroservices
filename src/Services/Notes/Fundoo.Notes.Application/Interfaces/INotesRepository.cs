using Fundoo.Notes.Domain.Entity;

namespace Fundoo.Notes.Application.Interfaces
{
    public interface INotesRepository
    {
        Task<Note?> GetByIdAsync(Guid noteId, Guid userId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<NoteLabel>> GetLabelsAsync(Guid noteId, Guid ownerId, CancellationToken cancellationToken = default);
        Task<NoteAccessSnapshot?> GetAccessSnapshotAsync(Guid noteId, Guid userId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Note>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default, int page = 1, int pageSize = 50);

        Task<IReadOnlyList<Note>> GetActiveAsync(Guid userId, CancellationToken cancellationToken = default, int page = 1, int pageSize = 50);

        Task<IReadOnlyList<Note>> GetArchivedAsync(Guid userId, CancellationToken cancellationToken = default, int page = 1, int pageSize = 50);

        Task<IReadOnlyList<Note>> GetTrashedAsync(Guid userId, CancellationToken cancellationToken = default, int page = 1, int pageSize = 50);

        Task<Note> CreateAsync(Note note, CancellationToken cancellationToken = default);

        Task<bool> UpdateAsync(Note note, CancellationToken cancellationToken = default);

        Task<bool> DeleteAsync(Guid noteId, Guid userId, CancellationToken cancellationToken = default);
    }
}
