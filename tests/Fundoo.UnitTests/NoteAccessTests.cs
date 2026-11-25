using Fundoo.Notes.Application.Interfaces;
using Fundoo.Notes.Application.Services;
using Fundoo.Notes.Domain.Entity;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Fundoo.UnitTests;

[TestFixture]
public sealed class NoteAccessTests
{
    private Guid _owner;
    private Guid _reader;
    private Guid _noteId;
    private AccessRepository _notes = null!;
    private AccessRepository _collaborators = null!;
    private TestCache _cache = null!;
    private NotesService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _owner = Guid.NewGuid(); _reader = Guid.NewGuid(); _noteId = Guid.NewGuid();
        _notes = new AccessRepository(_noteId, _owner);
        _collaborators = _notes;
        _cache = new TestCache();
        _service = new NotesService(_notes, _collaborators, _cache, NullLogger<NotesService>.Instance);
    }

    [Test]
    public async Task OwnerAndCollaboratorRepeatedlyReadSameCachedNote()
    {
        _notes.Permissions[_reader] = CollaboratorPermission.Read;
        var ownerFirst = await _service.GetByIdAsync(_noteId, _owner);
        var collaborator = await _service.GetByIdAsync(_noteId, _reader);
        var ownerAgain = await _service.GetByIdAsync(_noteId, _owner);
        Assert.That(collaborator!.Id, Is.EqualTo(ownerFirst!.Id));
        Assert.That(ownerAgain!.Title, Is.EqualTo(collaborator.Title));
        Assert.That(_notes.NoteReads, Is.EqualTo(1));
    }

    [Test]
    public async Task CollaboratorSeesSharedActiveNoteInNotesList()
    {
        _notes.Permissions[_reader] = CollaboratorPermission.Read;

        var notes = await _service.GetActiveAsync(_reader);

        Assert.That(notes.Select(note => note.Id), Does.Contain(_noteId));
    }

    [Test]
    public async Task ContentEditInvalidatesCacheAndReturnsLatestSavedContent()
    {
        _notes.Permissions[_reader] = CollaboratorPermission.Edit;
        await _service.GetByIdAsync(_noteId, _owner);
        var updated = await _service.UpdateAsync(new Note { Id = _noteId, UserId = _reader, Title = "Latest", Content = "Updated body" });
        var reread = await _service.GetByIdAsync(_noteId, _reader);
        Assert.That(updated, Is.True);
        Assert.That(reread!.Title, Is.EqualTo("Latest"));
        Assert.That(reread.Content, Is.EqualTo("Updated body"));
    }

    [Test]
    public async Task LabelChangesAreReloadedForOwnerAndCollaborator()
    {
        _notes.Permissions[_reader] = CollaboratorPermission.Read;
        await _service.GetByIdAsync(_noteId, _owner);
        _notes.Note.NoteLabels.Add(new NoteLabel { NoteId = _noteId, LabelId = Guid.NewGuid(), Label = new Label { Name = "Current" } });
        var collaboratorRead = await _service.GetByIdAsync(_noteId, _reader);
        Assert.That(collaboratorRead!.NoteLabels.Single().Label.Name, Is.EqualTo("Current"));
        _notes.Note.NoteLabels.Clear();
        var ownerRead = await _service.GetByIdAsync(_noteId, _owner);
        Assert.That(ownerRead!.NoteLabels, Is.Empty);
    }

    [Test]
    public async Task TrashRestoreAndDeleteHaveExpectedAccess()
    {
        Assert.That(await _service.SetArchivedAsync(_noteId, _owner, true), Is.True);
        Assert.That((await _service.GetByIdAsync(_noteId, _owner))!.IsArchived, Is.True);
        Assert.That(await _service.SetArchivedAsync(_noteId, _owner, false), Is.True);
        Assert.That(await _service.SetTrashedAsync(_noteId, _owner, true), Is.True);
        Assert.That(await _service.GetByIdAsync(_noteId, _owner), Is.Null);
        Assert.That(await _service.SetTrashedAsync(_noteId, _owner, false), Is.True);
        Assert.That(await _service.GetByIdAsync(_noteId, _owner), Is.Not.Null);
        Assert.That(await _service.SetTrashedAsync(_noteId, _owner, true), Is.True);
        Assert.That(await _service.DeleteAsync(_noteId, _owner), Is.True);
        Assert.That(await _service.GetByIdAsync(_noteId, _owner), Is.Null);
    }

    [Test]
    public async Task CollaboratorSeesArchivedStateAndLosesAccessWhileNoteIsTrashed()
    {
        _notes.Permissions[_reader] = CollaboratorPermission.Read;

        Assert.That(await _service.GetByIdAsync(_noteId, _reader), Is.Not.Null);
        Assert.That(await _service.SetArchivedAsync(_noteId, _owner, true), Is.True);
        Assert.That((await _service.GetByIdAsync(_noteId, _reader))!.IsArchived, Is.True);

        Assert.That(await _service.SetTrashedAsync(_noteId, _owner, true), Is.True);
        Assert.That(await _service.GetByIdAsync(_noteId, _reader), Is.Null);

        Assert.That(await _service.SetTrashedAsync(_noteId, _owner, false), Is.True);
        var restored = await _service.GetByIdAsync(_noteId, _reader);
        Assert.That(restored, Is.Not.Null);
        Assert.That(restored!.IsTrashed, Is.False);
        Assert.That(restored.IsArchived, Is.False);
    }

    [Test]
    public async Task PermanentDeletionRemovesAccessForOwnerAndCollaborator()
    {
        _notes.Permissions[_reader] = CollaboratorPermission.Read;
        Assert.That(await _service.SetTrashedAsync(_noteId, _owner, true), Is.True);

        Assert.That(await _service.DeleteAsync(_noteId, _owner), Is.True);
        Assert.That(await _service.GetByIdAsync(_noteId, _owner), Is.Null);
        Assert.That(await _service.GetByIdAsync(_noteId, _reader), Is.Null);
    }

    [Test]
    public async Task RemovingCollaboratorImmediatelyRevokesCachedAccess()
    {
        _notes.Permissions[_reader] = CollaboratorPermission.Read;
        Assert.That(await _service.GetByIdAsync(_noteId, _reader), Is.Not.Null);
        _notes.Permissions.Remove(_reader);
        Assert.That(await _service.GetByIdAsync(_noteId, _reader), Is.Null);
    }

    [Test]
    public async Task PermissionChangesTakeEffectImmediately()
    {
        _notes.Permissions[_reader] = CollaboratorPermission.Edit;
        Assert.That(await _service.UpdateAsync(new Note { Id = _noteId, UserId = _reader, Title = "Edited" }), Is.True);
        _notes.Permissions[_reader] = CollaboratorPermission.Read;
        Assert.That(await _service.UpdateAsync(new Note { Id = _noteId, UserId = _reader, Title = "Denied" }), Is.False);
        Assert.That(_notes.Note.Title, Is.EqualTo("Edited"));
    }

    [Test]
    public async Task ReadCollaboratorCannotEditButEditCollaboratorCan()
    {
        _notes.Permissions[_reader] = CollaboratorPermission.Read;
        Assert.That(await _service.GetByIdAsync(_noteId, _reader), Is.Not.Null);
        Assert.That(await _service.UpdateAsync(new Note { Id = _noteId, UserId = _reader, Title = "Denied" }), Is.False);
        Assert.That(_notes.Note.Title, Is.EqualTo("Original"));

        _notes.Permissions[_reader] = CollaboratorPermission.Edit;
        Assert.That(await _service.UpdateAsync(new Note { Id = _noteId, UserId = _reader, Title = "Allowed" }), Is.True);
        Assert.That((await _service.GetByIdAsync(_noteId, _reader))!.Title, Is.EqualTo("Allowed"));
    }

    [Test]
    public async Task CacheFailureFallsBackToDatabase()
    {
        _cache.ThrowOnRead = true; _cache.ThrowOnWrite = true;
        var value = await _service.GetByIdAsync(_noteId, _owner);
        Assert.That(value!.Title, Is.EqualTo("Original"));
        Assert.That(_notes.NoteReads, Is.EqualTo(1));
    }

    [Test]
    public void NotesDatabaseFailureIsPropagatedForApiErrorHandling()
    {
        _notes.ThrowOnAccessSnapshot = true;

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.GetByIdAsync(_noteId, _owner));
    }

    [Test]
    public async Task FailedInvalidationCannotReturnStaleNoteData()
    {
        await _service.GetByIdAsync(_noteId, _owner);
        _cache.ThrowOnRemove = true;
        Assert.That(await _service.UpdateAsync(new Note { Id = _noteId, UserId = _owner, Title = "Fresh" }), Is.True);
        Assert.That((await _service.GetByIdAsync(_noteId, _owner))!.Title, Is.EqualTo("Fresh"));
    }

    [Test]
    public async Task NonCollaboratorCannotReadNote()
    {
        Assert.That(await _service.GetByIdAsync(_noteId, Guid.NewGuid()), Is.Null);
        Assert.That(_notes.NoteReads, Is.Zero);
    }

    [Test]
    public async Task DuplicateCollaboratorIsRejected()
    {
        var service = new NoteCollaboratorsService(_collaborators, _cache, NullLogger<NoteCollaboratorsService>.Instance);
        Assert.That(await service.AddAsync(_noteId, _owner, _reader, CollaboratorPermission.Read, default), Is.True);
        Assert.That(await service.AddAsync(_noteId, _owner, _reader, CollaboratorPermission.Read, default), Is.False);
    }

    [Test]
    public async Task OwnerCannotRemoveThemselvesAsCollaborator()
    {
        var service = new NoteCollaboratorsService(_collaborators, _cache, NullLogger<NoteCollaboratorsService>.Instance);
        Assert.That(await service.RemoveAsync(_noteId, _owner, _owner, default), Is.False);
    }

    [Test]
    public void EmptyCollaboratorIdIsRejected()
    {
        var service = new NoteCollaboratorsService(_collaborators, _cache, NullLogger<NoteCollaboratorsService>.Instance);
        Assert.ThrowsAsync<ArgumentException>(async () => await service.AddAsync(_noteId, _owner, Guid.Empty, CollaboratorPermission.Read, default));
    }

    private sealed class TestCache : ICacheService
    {
        private readonly Dictionary<string, object> _values = new();
        public bool ThrowOnRead { get; set; }
        public bool ThrowOnWrite { get; set; }
        public bool ThrowOnRemove { get; set; }
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            if (ThrowOnRead) throw new InvalidOperationException("cache down");
            return Task.FromResult(_values.TryGetValue(key, out var value) ? (T)value : default);
        }
        public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            if (ThrowOnWrite) throw new InvalidOperationException("cache down");
            _values[key] = value!; return Task.CompletedTask;
        }
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        { if (ThrowOnRemove) throw new InvalidOperationException("cache down"); _values.Remove(key); return Task.CompletedTask; }
    }

    private sealed class AccessRepository : INotesRepository, INoteCollaboratorsRepository
    {
        public Note Note { get; }
        public int NoteReads { get; private set; }
        public bool ThrowOnAccessSnapshot { get; set; }
        public Dictionary<Guid, CollaboratorPermission> Permissions { get; } = new();
        public AccessRepository(Guid noteId, Guid ownerId) => Note = new Note { Id = noteId, UserId = ownerId, Title = "Original", Content = "Body", RowVersion = 1 };
        public Task<Guid?> GetOwnerIdAsync(Guid noteId, CancellationToken cancellationToken = default) => Task.FromResult<Guid?>(noteId == Note.Id ? Note.UserId : null);
        public Task<CollaboratorPermission?> GetPermissionAsync(Guid noteId, Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(Permissions.TryGetValue(userId, out var value) ? (CollaboratorPermission?)value : null);
        public Task<Note?> GetByIdAsync(Guid noteId, Guid userId, CancellationToken cancellationToken = default)
        { NoteReads++; return Task.FromResult<Note?>(noteId == Note.Id && (userId == Note.UserId || Permissions.ContainsKey(userId)) ? Copy(Note) : null); }
        public Task<IReadOnlyList<NoteLabel>> GetLabelsAsync(Guid noteId, Guid ownerId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<NoteLabel>>(Note.NoteLabels.ToList());
        public Task<NoteAccessSnapshot?> GetAccessSnapshotAsync(Guid noteId, Guid userId, CancellationToken cancellationToken = default)
        { if (ThrowOnAccessSnapshot) throw new InvalidOperationException("notes database unavailable"); if (noteId != Note.Id || (userId != Note.UserId && !Permissions.TryGetValue(userId, out _))) return Task.FromResult<NoteAccessSnapshot?>(null); Permissions.TryGetValue(userId, out var permission); return Task.FromResult<NoteAccessSnapshot?>(new NoteAccessSnapshot(Note.UserId, Note.RowVersion, userId == Note.UserId ? null : permission)); }
        private static Note Copy(Note source) => new() { Id = source.Id, UserId = source.UserId, Title = source.Title, Content = source.Content, Color = source.Color, IsPinned = source.IsPinned, IsArchived = source.IsArchived, IsTrashed = source.IsTrashed, CreatedAtUtc = source.CreatedAtUtc, UpdatedAtUtc = source.UpdatedAtUtc, RowVersion = source.RowVersion, NoteLabels = source.NoteLabels.ToList() };
        public Task<IReadOnlyList<Note>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default, int page = 1, int pageSize = 50) => Task.FromResult<IReadOnlyList<Note>>([]);
        public Task<IReadOnlyList<Note>> GetActiveAsync(Guid userId, CancellationToken cancellationToken = default, int page = 1, int pageSize = 50) => Task.FromResult<IReadOnlyList<Note>>(userId == Note.UserId || Permissions.ContainsKey(userId) ? [Copy(Note)] : []);
        public Task<IReadOnlyList<Note>> GetArchivedAsync(Guid userId, CancellationToken cancellationToken = default, int page = 1, int pageSize = 50) => Task.FromResult<IReadOnlyList<Note>>([]);
        public Task<IReadOnlyList<Note>> GetTrashedAsync(Guid userId, CancellationToken cancellationToken = default, int page = 1, int pageSize = 50) => Task.FromResult<IReadOnlyList<Note>>([]);
        public Task<Note> CreateAsync(Note note, CancellationToken cancellationToken = default) => Task.FromResult(note);
        public Task<bool> UpdateAsync(Note note, CancellationToken cancellationToken = default)
        { if (note.RowVersion != Note.RowVersion) return Task.FromResult(false); Note.RowVersion++; note.RowVersion = Note.RowVersion; Note.Title = note.Title; Note.Content = note.Content; Note.IsArchived = note.IsArchived; Note.IsTrashed = note.IsTrashed; Note.Color = note.Color; Note.IsPinned = note.IsPinned; return Task.FromResult(true); }
        public Task<bool> DeleteAsync(Guid noteId, Guid userId, CancellationToken cancellationToken = default)
        { if (Note.IsTrashed && userId == Note.UserId) { Note.Id = Guid.Empty; return Task.FromResult(true); } return Task.FromResult(false); }
        public Task<IReadOnlyList<NoteCollaborator>> GetAllAsync(Guid noteId, Guid ownerId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<NoteCollaborator>>([]);
        public Task<bool> AddAsync(Guid noteId, Guid ownerId, Guid userId, CollaboratorPermission permission, CancellationToken cancellationToken = default)
        { if (ownerId != Note.UserId || ownerId == userId || Permissions.ContainsKey(userId)) return Task.FromResult(false); Permissions[userId] = permission; return Task.FromResult(true); }
        public Task<bool> RemoveAsync(Guid noteId, Guid ownerId, Guid userId, CancellationToken cancellationToken = default)
        { return Task.FromResult(ownerId == Note.UserId && ownerId != userId && Permissions.Remove(userId)); }
        public Task<bool> SetPermissionAsync(Guid noteId, Guid ownerId, Guid userId, CollaboratorPermission permission, CancellationToken cancellationToken = default)
        { if (ownerId != Note.UserId || !Permissions.ContainsKey(userId)) return Task.FromResult(false); Permissions[userId] = permission; return Task.FromResult(true); }
    }
}
