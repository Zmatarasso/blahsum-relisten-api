using SimpleMigrations;

namespace Migrations;

[Migration(13, "Apply the body that upstream's migration 04 commented out")]
public class ApplyCommentedMigration04 : Migration
{
    protected override void Up()
    {
        // Upstream's 04_StopDeletingAllOnImport.cs has its real body wrapped
        // in /* */ and only runs `SELECT 1`. They rely on these schema
        // changes already being present in the production DB seed; for an
        // empty DB the API queries reference columns that don't exist
        // (notably source_tracks.is_orphaned, used by SourceService).
        // We replay it here, idempotent against partial state.

        // --- source_reviews.uuid (table is empty in our world; safe NOT NULL).
        Execute(@"
            ALTER TABLE source_reviews
              ADD COLUMN IF NOT EXISTS uuid uuid;

            UPDATE source_reviews
              SET uuid = md5(
                source_id || '::review::'
                || COALESCE('' || rating, 'NULL')
                || COALESCE('' || title, 'NULL')
                || COALESCE('' || author, 'NULL')
                || updated_at
              )::uuid
              WHERE uuid IS NULL;

            -- Drop dupes if any exist (no-op on empty table).
            DELETE FROM source_reviews
              WHERE uuid IN (
                SELECT uuid FROM source_reviews
                GROUP BY uuid HAVING COUNT(*) > 1
              );

            ALTER TABLE source_reviews
              ALTER COLUMN uuid SET NOT NULL;
        ");

        Execute(@"
            DO $$ BEGIN
                ALTER TABLE source_reviews ADD CONSTRAINT source_reviews_uuid UNIQUE (uuid);
            EXCEPTION WHEN duplicate_object THEN NULL; END $$;
        ");

        // --- source_tracks.is_orphaned + nullable parents + FK SET NULL.
        Execute(@"
            ALTER TABLE source_tracks
              ADD COLUMN IF NOT EXISTS is_orphaned BOOLEAN NOT NULL DEFAULT FALSE;

            ALTER TABLE source_tracks
              ALTER COLUMN source_set_id DROP NOT NULL,
              ALTER COLUMN source_id     DROP NOT NULL;
        ");

        // FK swaps: original has ON DELETE CASCADE; we want ON DELETE SET NULL
        // so deleting a set doesn't nuke tracks (which is_orphaned manages).
        Execute(@"
            DO $$ BEGIN
                ALTER TABLE source_tracks DROP CONSTRAINT source_tracks_set_id_fkey;
            EXCEPTION WHEN undefined_object THEN NULL; END $$;
            ALTER TABLE source_tracks
              ADD CONSTRAINT source_tracks_set_id_fkey
              FOREIGN KEY (source_set_id) REFERENCES source_sets(id)
              ON UPDATE CASCADE ON DELETE SET NULL;
        ");
        Execute(@"
            DO $$ BEGIN
                ALTER TABLE source_tracks DROP CONSTRAINT source_tracks_source_id_fkey;
            EXCEPTION WHEN undefined_object THEN NULL; END $$;
            ALTER TABLE source_tracks
              ADD CONSTRAINT source_tracks_source_id_fkey
              FOREIGN KEY (source_id) REFERENCES sources(id)
              ON UPDATE CASCADE ON DELETE SET NULL;
        ");

        // --- source_sets uniqueness, setlist_songs_plays uniqueness.
        Execute(@"
            DO $$ BEGIN
                ALTER TABLE source_sets
                  ADD CONSTRAINT source_sets_source_id_index_key UNIQUE (source_id, index);
            EXCEPTION WHEN duplicate_object THEN NULL; END $$;

            DO $$ BEGIN
                ALTER TABLE setlist_songs_plays
                  ADD CONSTRAINT setlist_songs_plays_song_id_show_id_key
                  UNIQUE (played_setlist_song_id, played_setlist_show_id);
            EXCEPTION WHEN duplicate_object THEN NULL; END $$;
        ");
    }

    protected override void Down()
    {
        throw new System.NotImplementedException();
    }
}
