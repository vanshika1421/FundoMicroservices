using Fundoo.Notes.Application.Interfaces;
using Fundoo.Notes.Domain.Entity;
using Microsoft.Extensions.Logging;

namespace Fundoo.Notes.Application.Services
{
    public class LabelsService : ILabelsService
    {
        private readonly ILabelsRepository _labelsRepository;
        private readonly ICacheService? _cache;
        private readonly Microsoft.Extensions.Logging.ILogger<LabelsService>? _logger;

        public LabelsService(ILabelsRepository labelsRepository)
        {
            _labelsRepository = labelsRepository;
        }

        public LabelsService(ILabelsRepository labelsRepository, ICacheService cache,
            Microsoft.Extensions.Logging.ILogger<LabelsService> logger)
        {
            _labelsRepository = labelsRepository;
            _cache = cache;
            _logger = logger;
        }

        public async Task<Label> CreateAsync(Guid userId, string name,
            CancellationToken cancellationToken = default)
        {
            ValidateName(name);

            var normalizedName = name.Trim();

            var label = new Label
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = normalizedName
            };

            return await _labelsRepository.CreateAsync(
                label, cancellationToken);
        }

        public async Task<IReadOnlyList<Label>> GetAllAsync(Guid userId,
            CancellationToken cancellationToken = default)
        {
            return await _labelsRepository.GetAllAsync(
                userId, cancellationToken);
        }

        public async Task<bool> UpdateAsync(Guid labelId, Guid userId, string name,
            CancellationToken cancellationToken = default)
        {
            ValidateName(name);

            var affected = await _labelsRepository.GetLinkedNoteIdsAsync(labelId, userId, cancellationToken);
            var updated = await _labelsRepository.UpdateAsync(
                labelId,
                userId,
                name.Trim(),
                cancellationToken);
            if (updated) await InvalidateAsync(affected, cancellationToken);
            return updated;
        }

        public async Task<bool> DeleteAsync(Guid labelId, Guid userId,
            CancellationToken cancellationToken = default)
        {
            var affected = await _labelsRepository.GetLinkedNoteIdsAsync(labelId, userId, cancellationToken);
            var deleted = await _labelsRepository.DeleteAsync(
                labelId, userId, cancellationToken);
            if (deleted) await InvalidateAsync(affected, cancellationToken);
            return deleted;
        }

        public async Task<bool> AttachToNoteAsync(Guid noteId, Guid labelId, Guid userId,
            CancellationToken cancellationToken = default)
        {
            var attached = await _labelsRepository.AttachToNoteAsync(
                noteId, labelId, userId, cancellationToken);
            if (attached) await InvalidateAsync([noteId], cancellationToken);
            return attached;
        }

        public async Task<bool> DetachFromNoteAsync(Guid noteId, Guid labelId, Guid userId,
            CancellationToken cancellationToken = default)
        {
            var detached = await _labelsRepository.DetachFromNoteAsync(
                noteId, labelId, userId, cancellationToken);
            if (detached) await InvalidateAsync([noteId], cancellationToken);
            return detached;
        }

        private async Task InvalidateAsync(IEnumerable<Guid> noteIds, CancellationToken token)
        {
            if (_cache is null) return;
            foreach (var id in noteIds)
            {
                try { await _cache.RemoveAsync($"note:{id:N}", token); }
                catch (Exception ex) { _logger?.LogWarning(ex, "Failed to invalidate note cache after label change for {NoteId}", id); }
            }
        }

        private static void ValidateName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Label name is required.");
            }

            if (name.Trim().Length > 100)
            {
                throw new ArgumentException(
                    "Label name cannot exceed 100 characters.");
            }
        }
    }
}
