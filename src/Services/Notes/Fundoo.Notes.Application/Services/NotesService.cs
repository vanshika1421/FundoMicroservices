using Fundoo.Notes.Application.Interfaces;
using Fundoo.Notes.Domain.Entity;
using Microsoft.Extensions.Logging;

namespace Fundoo.Notes.Application.Services
{
    public class NotesService : INotesService
    {
        private readonly INotesRepository _notesRepository;
        private readonly INoteCollaboratorsRepository? _collaborators;
        private readonly ICacheService? _cache;
        private readonly ILogger<NotesService>? _logger;

        public NotesService(INotesRepository notesRepository)
        {
            _notesRepository = notesRepository;
        }

        public NotesService(INotesRepository notesRepository, INoteCollaboratorsRepository collaborators,
            ICacheService cache, ILogger<NotesService> logger)
        {
            _notesRepository = notesRepository;
            _collaborators = collaborators;
            _cache = cache;
            _logger = logger;
        }

        public async Task<Note?> GetByIdAsync(Guid noteId, Guid userId,
            CancellationToken cancellationToken = default)
        {
            if (_collaborators is null || _cache is null)
                return await _notesRepository.GetByIdAsync(noteId, userId, cancellationToken);

            var access = await _notesRepository.GetAccessSnapshotAsync(noteId, userId, cancellationToken);
            if (access is null) return null;

            var key = $"note:{noteId:N}";
            try
            {
                var cached = await _cache.GetAsync<Note>(key, cancellationToken);
                if (cached is not null)
                {
                    if (cached.RowVersion == access.RowVersion)
                    {
                        if (cached.IsTrashed) return null;
                        cached.NoteLabels = (await _notesRepository.GetLabelsAsync(noteId, access.OwnerId, cancellationToken)).ToList();
                        return cached;
                    }
                }
            }
            catch (Exception ex) { _logger?.LogWarning(ex, "Note cache read failed for {NoteId}; reading database", noteId); }

            var note = await _notesRepository.GetByIdAsync(noteId, userId, cancellationToken);
            if (note is null || note.IsTrashed) return null;
            try { await _cache.SetAsync(key, note, TimeSpan.FromMinutes(2), cancellationToken); }
            catch (Exception ex) { _logger?.LogWarning(ex, "Note cache write failed for {NoteId}; returning database result", noteId); }
            return note;
        }

        public async Task<IReadOnlyList<Note>> GetAllAsync(Guid userId,
            CancellationToken cancellationToken = default, int page = 1, int pageSize = 50)
        {
            (page, pageSize) = NormalizePage(page, pageSize);
            return await _notesRepository.GetAllAsync(userId, cancellationToken, page, pageSize);
        }

        public async Task<IReadOnlyList<Note>> GetActiveAsync(Guid userId,
            CancellationToken cancellationToken = default, int page = 1, int pageSize = 50)
        {
            (page, pageSize) = NormalizePage(page, pageSize);
            return await _notesRepository.GetActiveAsync(userId, cancellationToken, page, pageSize);
        }

        public async Task<IReadOnlyList<Note>> GetArchivedAsync(Guid userId,
            CancellationToken cancellationToken = default, int page = 1, int pageSize = 50)
        {
            (page, pageSize) = NormalizePage(page, pageSize);
            return await _notesRepository.GetArchivedAsync(userId, cancellationToken, page, pageSize);
        }

        public async Task<IReadOnlyList<Note>> GetTrashedAsync(Guid userId,
            CancellationToken cancellationToken = default, int page = 1, int pageSize = 50)
        {
            (page, pageSize) = NormalizePage(page, pageSize);
            return await _notesRepository.GetTrashedAsync(userId, cancellationToken, page, pageSize);
        }

        private static (int Page, int PageSize) NormalizePage(int page, int pageSize) =>
            (Math.Max(1, page), Math.Clamp(pageSize, 1, 100));

        public async Task<Note> CreateAsync(Note note,
            CancellationToken cancellationToken = default)
        {
            ValidateTitle(note.Title);

            // A newly created note starts as active.
            note.IsArchived = false;
            note.IsTrashed = false;

            return await _notesRepository.CreateAsync(
                note, cancellationToken);
        }

        public async Task<bool> UpdateAsync(Note note,
            CancellationToken cancellationToken = default)
        {
            ValidateTitle(note.Title);

            // Verify that the note belongs to the caller.
            var existingNote = await _notesRepository.GetByIdAsync(note.Id, note.UserId, cancellationToken);

            if (existingNote is null)
                return false;

            if (_collaborators is not null && existingNote.UserId != note.UserId &&
                await _collaborators.GetPermissionAsync(note.Id, note.UserId, cancellationToken) != CollaboratorPermission.Edit)
                return false;

            // Do not allow normal editing of trashed notes.
            if (existingNote.IsTrashed)
                return false;

            // Update only editable fields.
            existingNote.Title = note.Title.Trim();
            existingNote.Content = note.Content;
            existingNote.Color = note.Color;
            existingNote.IsPinned = note.IsPinned;

            var saved = await _notesRepository.UpdateAsync(existingNote, cancellationToken);
            if (saved) await InvalidateNoteAsync(note.Id, cancellationToken);
            return saved;
        }

        public async Task<bool> SetArchivedAsync(Guid noteId, Guid userId, bool isArchived,
            CancellationToken cancellationToken = default)
        {
            var note = await _notesRepository.GetByIdAsync(
                noteId, userId, cancellationToken);

            if (note is null || (_collaborators is not null && note.UserId != userId))
                return false;

            // Trashed notes must be restored before archiving.
            if (note.IsTrashed)
                return false;

            note.IsArchived = isArchived;

            var saved = await _notesRepository.UpdateAsync(note, cancellationToken);
            if (saved) await InvalidateNoteAsync(noteId, cancellationToken);
            return saved;
        }

        public async Task<bool> SetTrashedAsync(Guid noteId, Guid userId, bool isTrashed,
            CancellationToken cancellationToken = default)
        {
            var note = await _notesRepository.GetByIdAsync(
                noteId, userId, cancellationToken);

            if (note is null || (_collaborators is not null && note.UserId != userId))
                return false;

            if (isTrashed)
            {
                // Move the note to trash.
                note.IsTrashed = true;
            }
            else
            {
                // Restore the note to the active list.
                note.IsTrashed = false;
                note.IsArchived = false;
            }

            var saved = await _notesRepository.UpdateAsync(note, cancellationToken);
            if (saved) await InvalidateNoteAsync(noteId, cancellationToken);
            return saved;
        }

        public async Task<bool> DeleteAsync(Guid noteId, Guid userId,
            CancellationToken cancellationToken = default)
        {
            var note = await _notesRepository.GetByIdAsync(
                noteId, userId, cancellationToken);

            // Missing notes and non-trashed notes cannot be deleted.
            if (note is null || !note.IsTrashed)
                return false;

            var deleted = await _notesRepository.DeleteAsync(noteId, userId, cancellationToken);
            if (deleted) await InvalidateNoteAsync(noteId, cancellationToken);
            return deleted;
        }

        private async Task InvalidateNoteAsync(Guid noteId, CancellationToken cancellationToken)
        {
            if (_cache is null) return;
            try { await _cache.RemoveAsync($"note:{noteId:N}", cancellationToken); }
            catch (Exception ex) { _logger?.LogWarning(ex, "Note cache invalidation failed for {NoteId}", noteId); }
        }

        private static void ValidateTitle(string? title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException(
                    "Note title is required.");
            }
        }
    }
}
