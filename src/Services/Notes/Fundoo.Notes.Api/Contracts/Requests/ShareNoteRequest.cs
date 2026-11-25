using Fundoo.Notes.Domain.Entity;

namespace Fundoo.Notes.Api.Contracts.Requests;

public sealed class ShareNoteRequest
{
    public Guid UserId { get; set; }
    public CollaboratorPermission Permission { get; set; }
}
