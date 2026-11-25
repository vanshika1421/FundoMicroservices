using Fundoo.Notes.Application.Interfaces;
using Fundoo.Notes.Application.Services;
using Fundoo.Notes.Domain.Entity;
using NUnit.Framework;

namespace Fundoo.UnitTests;

[TestFixture]
public sealed class NotesServiceTests
{
    private readonly FakeNotesRepository _repository = new();
    private NotesService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _repository.Reset();
        _service = new NotesService(_repository);
    }

    [Test]
    public async Task CreateAsync_RejectsBlankTitle()
    {
        var note = new Note { Title = "   " };

        var exception = Assert.ThrowsAsync<ArgumentException>(
            async () => await _service.CreateAsync(note));

        Assert.That(exception!.Message, Does.Contain("title is required"));
        Assert.That(_repository.CreateCalls, Is.Zero);
    }

    [Test]
    public async Task CreateAsync_InitializesNewNoteAsActive()
    {
        var note = new Note
        {
            Title = "  Grocery list  ",
            IsArchived = true,
            IsTrashed = true
        };

        await _service.CreateAsync(note);

        Assert.That(note.Title, Is.EqualTo("  Grocery list  "));
        Assert.That(note.IsArchived, Is.False);
        Assert.That(note.IsTrashed, Is.False);
        Assert.That(_repository.CreateCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task UpdateAsync_RejectsNotesInTrash()
    {
        var existing = new Note { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Title = "Old", IsTrashed = true };
        _repository.Notes.Add(existing);

        var updated = await _service.UpdateAsync(new Note
        {
            Id = existing.Id,
            UserId = existing.UserId,
            Title = "New"
        });

        Assert.That(updated, Is.False);
        Assert.That(existing.Title, Is.EqualTo("Old"));
        Assert.That(_repository.UpdateCalls, Is.Zero);
    }

    [Test]
    public async Task DeleteAsync_RequiresNoteToBeTrashed()
    {
        var note = new Note { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        _repository.Notes.Add(note);

        var deleted = await _service.DeleteAsync(note.Id, note.UserId);

        Assert.That(deleted, Is.False);
        Assert.That(_repository.DeleteCalls, Is.Zero);
    }

    [Test]
    public async Task SetTrashedAsync_RestoringNoteAlsoUnarchivesIt()
    {
        var note = new Note
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            IsTrashed = true,
            IsArchived = true
        };
        _repository.Notes.Add(note);

        var restored = await _service.SetTrashedAsync(note.Id, note.UserId, false);

        Assert.That(restored, Is.True);
        Assert.That(note.IsTrashed, Is.False);
        Assert.That(note.IsArchived, Is.False);
        Assert.That(_repository.UpdateCalls, Is.EqualTo(1));
    }

    private sealed class FakeNotesRepository : INotesRepository
    {
        public List<Note> Notes { get; } = [];
        public int CreateCalls { get; private set; }
        public int UpdateCalls { get; private set; }
        public int DeleteCalls { get; private set; }

        public void Reset()
        {
            Notes.Clear();
            CreateCalls = UpdateCalls = DeleteCalls = 0;
        }

        public Task<Note?> GetByIdAsync(Guid noteId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Notes.FirstOrDefault(n => n.Id == noteId && n.UserId == userId));
        public Task<IReadOnlyList<NoteLabel>> GetLabelsAsync(Guid noteId, Guid ownerId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<NoteLabel>>([]);
        public Task<NoteAccessSnapshot?> GetAccessSnapshotAsync(Guid noteId, Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<NoteAccessSnapshot?>(null);
        public Task<IReadOnlyList<Note>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default, int page = 1, int pageSize = 50) =>
            Task.FromResult<IReadOnlyList<Note>>(Notes.Where(n => n.UserId == userId).ToList());
        public Task<IReadOnlyList<Note>> GetActiveAsync(Guid userId, CancellationToken cancellationToken = default, int page = 1, int pageSize = 50) =>
            Task.FromResult<IReadOnlyList<Note>>(Notes.Where(n => n.UserId == userId && !n.IsArchived && !n.IsTrashed).ToList());
        public Task<IReadOnlyList<Note>> GetArchivedAsync(Guid userId, CancellationToken cancellationToken = default, int page = 1, int pageSize = 50) =>
            Task.FromResult<IReadOnlyList<Note>>(Notes.Where(n => n.UserId == userId && n.IsArchived && !n.IsTrashed).ToList());
        public Task<IReadOnlyList<Note>> GetTrashedAsync(Guid userId, CancellationToken cancellationToken = default, int page = 1, int pageSize = 50) =>
            Task.FromResult<IReadOnlyList<Note>>(Notes.Where(n => n.UserId == userId && n.IsTrashed).ToList());
        public Task<Note> CreateAsync(Note note, CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            Notes.Add(note);
            return Task.FromResult(note);
        }
        public Task<bool> UpdateAsync(Note note, CancellationToken cancellationToken = default)
        {
            UpdateCalls++;
            return Task.FromResult(true);
        }
        public Task<bool> DeleteAsync(Guid noteId, Guid userId, CancellationToken cancellationToken = default)
        {
            DeleteCalls++;
            return Task.FromResult(true);
        }
    }
}
