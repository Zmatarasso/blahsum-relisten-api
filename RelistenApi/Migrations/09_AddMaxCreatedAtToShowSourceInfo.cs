using SimpleMigrations;

namespace Migrations;

[Migration(9, "Add max_created_at to show_source_information")]
public class AddMaxCreatedAtToShowSourceInfo : Migration
{
    protected override void Up()
    {
        // blahsum-relisten: upstream production has `show_source_information` as a
        // regular table that the importers UPDATE/DELETE against, but migration 01
        // creates it as a MATERIALIZED VIEW and the conversion to a table never made
        // it into the repo. Replace it with a regular table here before doing the
        // upstream column-add work. Column types match the matview's source columns
        // (see sources schema in 01_Schema.cs).
        Execute(@"
            DROP MATERIALIZED VIEW IF EXISTS show_source_information CASCADE;

            CREATE TABLE IF NOT EXISTS show_source_information (
                show_id                 bigint                       NOT NULL,
                max_updated_at          timestamp with time zone,
                source_count            bigint,
                artist_id               integer,
                max_avg_rating_weighted real,
                has_soundboard_source   boolean,
                has_flac                boolean
            );
            CREATE UNIQUE INDEX IF NOT EXISTS show_source_information_show_id_uidx
                ON show_source_information (show_id);
        ");

        Execute(@"
            ALTER TABLE show_source_information ADD COLUMN max_created_at timestamp with time zone;

            UPDATE show_source_information ssi
            SET max_created_at = (
                SELECT MAX(created_at) FROM sources WHERE show_id = ssi.show_id
            );

            -- Delete orphaned rows that have no matching sources (stale data)
            DELETE FROM show_source_information
            WHERE max_created_at IS NULL;

            ALTER TABLE show_source_information ALTER COLUMN max_created_at SET NOT NULL;
        ");
    }

    protected override void Down()
    {
        Execute(@"
            ALTER TABLE show_source_information DROP COLUMN max_created_at;
        ");
    }
}
