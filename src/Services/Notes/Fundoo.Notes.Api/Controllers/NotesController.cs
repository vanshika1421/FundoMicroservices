
using Fundoo.Notes.Api.Contracts.Requests;
using Fundoo.Notes.Api.Contracts.Response;
using Fundoo.Notes.Application.Interfaces;
using Fundoo.EventContracts.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Fundoo.Notes.Api.Authorization;
using Fundoo.Notes.Domain.Entity;

namespace Fundoo.Notes.Api.Controllers
{
    [ApiController]
    [Route("api/notes")]
    [Authorize]
    public class NotesController : ControllerBase
    {
        private readonly INotesService _notesService;
        private readonly INoteEventPublisher _noteEventPublisher;
        private readonly INoteCollaboratorsService _collaboratorsService;
        private readonly IAuthorizationService _authorizationService;

        public NotesController(
            INotesService notesService,
            INoteEventPublisher noteEventPublisher,
            INoteCollaboratorsService collaboratorsService,
            IAuthorizationService authorizationService)
        {
            _notesService = notesService;
            _noteEventPublisher = noteEventPublisher;
            _collaboratorsService = collaboratorsService;
            _authorizationService = authorizationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetActiveNotes(
            int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { message = "Invalid user token." });

            var notes = await _notesService.GetActiveAsync(
                userId, cancellationToken, page, pageSize);

            var response = notes
                .Select(MapToResponse)
                .ToList();

            return Ok(new
            {
                isSuccess = true,
                message = "Active notes retrieved successfully.",
                data = response
            });
        }

        [HttpGet("archived")]
        public async Task<IActionResult> GetArchivedNotes(
            int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { message = "Invalid user ID in token." });

            var notes = await _notesService.GetArchivedAsync(
                userId, cancellationToken, page, pageSize);

            var response = notes.Select(MapToResponse).ToList();

            return Ok(new
            {
                isSuccess = true,
                message = "Archived notes retrieved successfully.",
                data = response
            });
        }

        [HttpGet("trash")]
        public async Task<IActionResult> GetTrashedNotes(
            int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { message = "Invalid user ID in token." });

            var notes = await _notesService.GetTrashedAsync(
                userId, cancellationToken, page, pageSize);

            var response = notes.Select(MapToResponse).ToList();

            return Ok(new
            {
                isSuccess = true,
                message = "Trashed notes retrieved successfully.",
                data = response
            });
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetNoteById(
            Guid id,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { message = "Invalid user ID in token." });

            var note = await _notesService.GetByIdAsync(
                id, userId, cancellationToken);

            if (note == null)
                return NotFound(new { message = "Note not found." });
            if (!(await _authorizationService.AuthorizeAsync(User, note, new NoteReadRequirement())).Succeeded)
                return NotFound(new { message = "Note not found." });

            return Ok(new
            {
                isSuccess = true,
                message = "Note retrieved successfully.",
                data = MapToResponse(note)
            });
        }

        [HttpPost]
        public async Task<IActionResult> CreateNote(
            [FromBody] CreateNoteRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new
                {
                    message = "Invalid user ID in token."
                });
            }

            var note = new Note
            {
                UserId = userId,
                Title = request.Title?.Trim() ?? string.Empty,
                Content = request.Content ?? string.Empty,
                Color = request.Color,
                IsPinned = request.IsPinned,
                IsArchived = false,
                IsTrashed = false
            };

            var recipientEmail = User.FindFirst(ClaimTypes.Email)?.Value
                ?? User.FindFirst("email")?.Value;

            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                return Unauthorized(new
                {
                    message = "Email claim missing from access token."
                });
            }

            var createdNote = await _notesService.CreateAsync(
                note, cancellationToken);

            var noteCreatedEvent = new NoteEvent
            {
                EventType = "notes.created.v1",
                NoteId = createdNote.Id,
                UserId = createdNote.UserId,
                RecipientEmail = recipientEmail,
                Title = createdNote.Title
            };

            await _noteEventPublisher.PublishCreatedAsync(
                noteCreatedEvent,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetNoteById),
                new { id = createdNote.Id },
                new
                {
                    isSuccess = true,
                    message = "Note created successfully.",
                    data = MapToResponse(createdNote)
                });
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateNote(
            Guid id,
            [FromBody] UpdateNoteRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new
                {
                    message = "Invalid user ID in token."
                });
            }

            var recipientEmail = User.FindFirst(ClaimTypes.Email)?.Value
                ?? User.FindFirst("email")?.Value;
            if (string.IsNullOrWhiteSpace(recipientEmail))
                return Unauthorized(new { message = "Email claim missing from access token." });

            var note = new Note
            {
                Id = id,
                UserId = userId,
                Title = request.Title?.Trim() ?? string.Empty,
                Content = request.Content ?? string.Empty,
                Color = request.Color,
                IsPinned = request.IsPinned
            };

            var current = await _notesService.GetByIdAsync(id, userId, cancellationToken);
            if (current is null) return NotFound(new { message = "Note not found." });
            if (!(await _authorizationService.AuthorizeAsync(User, current, new NoteEditRequirement())).Succeeded)
                return Forbid();

            try
            {
                var updated = await _notesService.UpdateAsync(
                    note, cancellationToken);

                if (!updated)
                {
                    return NotFound(new
                    {
                        message = "Note not found or cannot be updated."
                    });
                }

                await _noteEventPublisher.PublishUpdatedAsync(new NoteEvent
                {
                    EventType = "notes.updated.v1",
                    NoteId = note.Id,
                    UserId = note.UserId,
                    RecipientEmail = recipientEmail,
                    Title = note.Title
                }, cancellationToken);

                return Ok(new
                {
                    isSuccess = true,
                    message = "Note updated successfully."
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
        }

        [HttpPut("{id:guid}/archive")]
        public Task<IActionResult> ArchiveNote(
            Guid id,
            CancellationToken cancellationToken)
        {
            return ChangeArchiveStatus(id, true, cancellationToken);
        }

        [HttpPut("{id:guid}/unarchive")]
        public Task<IActionResult> UnarchiveNote(
            Guid id,
            CancellationToken cancellationToken)
        {
            return ChangeArchiveStatus(id, false, cancellationToken);
        }

        private async Task<IActionResult> ChangeArchiveStatus(
            Guid id,
            bool isArchived,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { message = "Invalid user ID in token." });

            var updated = await _notesService.SetArchivedAsync(
                id, userId, isArchived, cancellationToken);

            if (!updated)
            {
                return NotFound(new
                {
                    message = "Note not found or cannot be archived."
                });
            }

            return Ok(new
            {
                isSuccess = true,
                message = isArchived
                    ? "Note archived successfully."
                    : "Note unarchived successfully."
            });
        }

        [HttpPut("{id:guid}/trash")]
        public Task<IActionResult> TrashNote(
            Guid id,
            CancellationToken cancellationToken)
        {
            return ChangeTrashStatus(id, true, cancellationToken);
        }

        [HttpPut("{id:guid}/restore")]
        public Task<IActionResult> RestoreNote(
            Guid id,
            CancellationToken cancellationToken)
        {
            return ChangeTrashStatus(id, false, cancellationToken);
        }

        private async Task<IActionResult> ChangeTrashStatus(
            Guid id,
            bool isTrashed,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { message = "Invalid user ID in token." });

            var updated = await _notesService.SetTrashedAsync(
                id, userId, isTrashed, cancellationToken);

            if (!updated)
            {
                return NotFound(new
                {
                    message = "Note not found or cannot be moved."
                });
            }

            return Ok(new
            {
                isSuccess = true,
                message = isTrashed
                    ? "Note moved to trash successfully."
                    : "Note restored successfully."
            });
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteNote(
            Guid id,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { message = "Invalid user ID in token." });

            var deleted = await _notesService.DeleteAsync(
                id, userId, cancellationToken);

            if (!deleted)
            {
                return NotFound(new
                {
                    message = "Note not found or must be moved to trash before deletion."
                });
            }

            return Ok(new
            {
                isSuccess = true,
                message = "Note permanently deleted."
            });
        }

        [HttpGet("{id:guid}/collaborators")]
        public async Task<IActionResult> GetCollaborators(Guid id, CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var ownerId)) return Unauthorized();
            var collaborators = await _collaboratorsService.GetAllAsync(id, ownerId, cancellationToken);
            return Ok(collaborators.Select(c => new { c.UserId, c.Permission, c.CreatedAtUtc }));
        }

        [HttpPost("{id:guid}/collaborators")]
        public async Task<IActionResult> AddCollaborator(Guid id, [FromBody] ShareNoteRequest request, CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var ownerId)) return Unauthorized();
            var added = await _collaboratorsService.AddAsync(id, ownerId, request.UserId, request.Permission, cancellationToken);
            return added ? StatusCode(StatusCodes.Status201Created) : Conflict(new { message = "Note is not owned by you, user already has access, or the user ID is invalid." });
        }

        [HttpPut("{id:guid}/collaborators/{userId:guid}")]
        public async Task<IActionResult> SetCollaboratorPermission(Guid id, Guid userId, [FromBody] ShareNoteRequest request, CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var ownerId)) return Unauthorized();
            if (request.UserId != Guid.Empty && request.UserId != userId) return BadRequest();
            var changed = await _collaboratorsService.SetPermissionAsync(id, ownerId, userId, request.Permission, cancellationToken);
            return changed ? NoContent() : NotFound();
        }

        [HttpDelete("{id:guid}/collaborators/{userId:guid}")]
        public async Task<IActionResult> RemoveCollaborator(Guid id, Guid userId, CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var ownerId)) return Unauthorized();
            var removed = await _collaboratorsService.RemoveAsync(id, ownerId, userId, cancellationToken);
            return removed ? NoContent() : NotFound();
        }

        private bool TryGetUserId(out Guid userId)
        {
            var userIdClaim = User.FindFirst("sub")
                              ?? User.FindFirst(ClaimTypes.NameIdentifier);

            return Guid.TryParse(userIdClaim?.Value, out userId);
        }

        private static NoteResponse MapToResponse(Note note)
        {
            return new NoteResponse
            {
                Id = note.Id,
                UserId = note.UserId,
                Title = note.Title,
                Content = note.Content,
                Color = note.Color,
                IsPinned = note.IsPinned,
                IsArchived = note.IsArchived,
                IsTrashed = note.IsTrashed,
                CreatedAtUtc = note.CreatedAtUtc,
                UpdatedAtUtc = note.UpdatedAtUtc,

                Labels = note.NoteLabels
                    .Where(nl => nl.Label != null)
                    .Select(nl => new NoteLabelResponse
                    {
                        Id = nl.Label!.Id,
                        Name = nl.Label.Name
                    })
                    .ToList()
            };
        }
    }
}
