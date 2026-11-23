-- Run this script against the NotesDb database before starting Fundoo.Notes.Api.
-- User IDs intentionally have no foreign key because UserDb is a separate database.

CREATE TABLE IF NOT EXISTS notes (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL,
    title text NOT NULL,
    content text NOT NULL,
    color text NULL,
    is_pinned boolean NOT NULL DEFAULT false,
    is_archived boolean NOT NULL DEFAULT false,
    is_trashed boolean NOT NULL DEFAULT false,
    created_at_utc timestamp with time zone NOT NULL,
    updated_at_utc timestamp with time zone NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_notes_user_created
    ON notes (user_id, created_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_notes_user_active
    ON notes (user_id, is_pinned DESC, created_at_utc DESC)
    WHERE is_archived = false AND is_trashed = false;

CREATE TABLE IF NOT EXISTS labels (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL,
    name text NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_labels_user_name
    ON labels (user_id, name);

CREATE TABLE IF NOT EXISTS note_labels (
    note_id uuid NOT NULL REFERENCES notes (id) ON DELETE CASCADE,
    label_id uuid NOT NULL REFERENCES labels (id) ON DELETE CASCADE,
    PRIMARY KEY (note_id, label_id)
);

CREATE INDEX IF NOT EXISTS ix_note_labels_label
    ON note_labels (label_id);
