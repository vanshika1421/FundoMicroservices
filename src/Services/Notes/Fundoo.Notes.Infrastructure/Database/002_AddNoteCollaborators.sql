-- Apply only to a local NotesDb. User IDs are in a separate database, so no user FK is possible.
ALTER TABLE notes ADD COLUMN IF NOT EXISTS row_version bigint NOT NULL DEFAULT 1;

CREATE TABLE IF NOT EXISTS note_collaborators (
    note_id uuid NOT NULL REFERENCES notes (id) ON DELETE CASCADE,
    user_id uuid NOT NULL,
    permission integer NOT NULL CHECK (permission IN (0, 1)),
    created_at_utc timestamp with time zone NOT NULL,
    CONSTRAINT pk_note_collaborators PRIMARY KEY (note_id, user_id)
);

CREATE INDEX IF NOT EXISTS ix_note_collaborators_user_id
    ON note_collaborators (user_id);
