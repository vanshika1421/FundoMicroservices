using Fundoo.Notes.Application.Interfaces;
using Fundoo.Notes.Application.Services;
using Fundoo.Notes.Domain.Entity;
using NUnit.Framework;

namespace Fundoo.UnitTests;

[TestFixture]
public sealed class LabelsServiceTests
{
    [TestCase("")]
    [TestCase("   ")]
    public void CreateAsync_RejectsBlankName(string name)
    {
        var service = new LabelsService(new FakeLabelsRepository());

        Assert.ThrowsAsync<ArgumentException>(async () => await service.CreateAsync(Guid.NewGuid(), name));
    }

    [Test]
    public void CreateAsync_RejectsNameLongerThan100Characters()
    {
        var service = new LabelsService(new FakeLabelsRepository());

        Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.CreateAsync(Guid.NewGuid(), new string('x', 101)));
    }

    [Test]
    public async Task CreateAsync_TrimsValidNameBeforeSaving()
    {
        var repository = new FakeLabelsRepository();
        var service = new LabelsService(repository);

        var label = await service.CreateAsync(Guid.NewGuid(), "  Work  ");

        Assert.That(label.Name, Is.EqualTo("Work"));
        Assert.That(repository.SavedLabel, Is.SameAs(label));
    }

    private sealed class FakeLabelsRepository : ILabelsRepository
    {
        public Label? SavedLabel { get; private set; }
        public Task<Label> CreateAsync(Label label, CancellationToken cancellationToken = default)
        {
            SavedLabel = label;
            return Task.FromResult(label);
        }
        public Task<IReadOnlyList<Label>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Label>>([]);
        public Task<bool> UpdateAsync(Guid labelId, Guid userId, string name, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> DeleteAsync(Guid labelId, Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> AttachToNoteAsync(Guid noteId, Guid labelId, Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> DetachFromNoteAsync(Guid noteId, Guid labelId, Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlyList<Guid>> GetLinkedNoteIdsAsync(Guid labelId, Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Guid>>([]);
    }
}
