using Fundoo.Notes.Application.Interfaces;
using Fundoo.Notes.Domain.Entity;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Fundoo.Notes.Api.Authorization;

public sealed class NoteReadRequirement : IAuthorizationRequirement { }
public sealed class NoteEditRequirement : IAuthorizationRequirement { }
public sealed class NoteOwnerRequirement : IAuthorizationRequirement { }

public sealed class NoteAuthorizationHandler(INoteCollaboratorsRepository collaborators)
    : AuthorizationHandler<IAuthorizationRequirement, Note>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        IAuthorizationRequirement requirement, Note note)
    {
        if (!Guid.TryParse(context.User.FindFirst("sub")?.Value ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId)) return;
        if (requirement is NoteOwnerRequirement && userId == note.UserId) context.Succeed(requirement);
        if (requirement is NoteReadRequirement or NoteEditRequirement)
        {
            if (userId == note.UserId) { context.Succeed(requirement); return; }
            var permission = await collaborators.GetPermissionAsync(note.Id, userId);
            if (permission == CollaboratorPermission.Edit ||
                (permission == CollaboratorPermission.Read && requirement is NoteReadRequirement))
                context.Succeed(requirement);
        }
    }
}
