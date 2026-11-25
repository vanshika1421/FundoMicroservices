using Fundoo.Notes.Application.Interfaces;
using Fundoo.Notes.Domain.Entity;
using Fundoo.Notes.Infrastructure.Data;
using Npgsql;

namespace Fundoo.Notes.Infrastructure.Repositories;

public sealed class NoteCollaboratorsRepository(NotesDbConnectionFactory connectionFactory) : INoteCollaboratorsRepository
{
    public async Task<Guid?> GetOwnerIdAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection(); await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT user_id FROM notes WHERE id=@noteId;", connection);
        command.Parameters.AddWithValue("noteId", noteId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? null : (Guid)value;
    }
    public async Task<CollaboratorPermission?> GetPermissionAsync(Guid noteId, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        const string sql = "SELECT permission FROM note_collaborators WHERE note_id = @noteId AND user_id = @userId;";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("noteId", noteId);
        command.Parameters.AddWithValue("userId", userId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : (CollaboratorPermission)Convert.ToInt32(result);
    }

    public async Task<IReadOnlyList<NoteCollaborator>> GetAllAsync(Guid noteId, Guid ownerId, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        const string sql = "SELECT c.note_id, c.user_id, c.permission, c.created_at_utc FROM note_collaborators c JOIN notes n ON n.id=c.note_id WHERE c.note_id=@noteId AND n.user_id=@ownerId ORDER BY c.created_at_utc;";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("noteId", noteId);
        command.Parameters.AddWithValue("ownerId", ownerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var values = new List<NoteCollaborator>();
        while (await reader.ReadAsync(cancellationToken))
            values.Add(new NoteCollaborator { NoteId = reader.GetGuid(0), UserId = reader.GetGuid(1), Permission = (CollaboratorPermission)reader.GetInt32(2), CreatedAtUtc = reader.GetDateTime(3) });
        return values;
    }

    public async Task<bool> AddAsync(Guid noteId, Guid ownerId, Guid userId, CollaboratorPermission permission, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        const string sql = "INSERT INTO note_collaborators(note_id,user_id,permission,created_at_utc) SELECT id,@userId,@permission,NOW() FROM notes WHERE id=@noteId AND user_id=@ownerId AND @ownerId<>@userId ON CONFLICT(note_id,user_id) DO NOTHING;";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("noteId", noteId); command.Parameters.AddWithValue("ownerId", ownerId);
        command.Parameters.AddWithValue("userId", userId); command.Parameters.AddWithValue("permission", (int)permission);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<bool> RemoveAsync(Guid noteId, Guid ownerId, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection(); await connection.OpenAsync(cancellationToken);
        const string sql = "DELETE FROM note_collaborators c USING notes n WHERE c.note_id=n.id AND c.note_id=@noteId AND c.user_id=@userId AND n.user_id=@ownerId AND @ownerId<>@userId;";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("noteId", noteId); command.Parameters.AddWithValue("ownerId", ownerId); command.Parameters.AddWithValue("userId", userId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<bool> SetPermissionAsync(Guid noteId, Guid ownerId, Guid userId, CollaboratorPermission permission, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection(); await connection.OpenAsync(cancellationToken);
        const string sql = "UPDATE note_collaborators c SET permission=@permission FROM notes n WHERE c.note_id=n.id AND c.note_id=@noteId AND c.user_id=@userId AND n.user_id=@ownerId;";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("noteId", noteId); command.Parameters.AddWithValue("ownerId", ownerId); command.Parameters.AddWithValue("userId", userId); command.Parameters.AddWithValue("permission", (int)permission);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }
}
