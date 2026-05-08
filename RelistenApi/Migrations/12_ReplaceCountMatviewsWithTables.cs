using SimpleMigrations;

namespace Migrations;

[Migration(12, "Replace venue_show_counts and source_review_counts matviews with tables")]
public class ReplaceCountMatviewsWithTables : Migration
{
    protected override void Up()
    {
        // Same pattern as migration 09: upstream production runs these as
        // regular TABLEs (the importers INSERT INTO them, and unpopulated
        // materialized views throw 55000 "has not been populated" on read).
        // The conversion never made it into the repo. Replace here.

        Execute(@"
            DROP MATERIALIZED VIEW IF EXISTS source_review_counts CASCADE;

            CREATE TABLE IF NOT EXISTS source_review_counts (
                source_id                    bigint NOT NULL,
                source_review_max_updated_at timestamp with time zone,
                source_review_count          bigint
            );
            CREATE UNIQUE INDEX IF NOT EXISTS source_review_counts_source_id_uidx
                ON source_review_counts (source_id);
        ");

        Execute(@"
            DROP MATERIALIZED VIEW IF EXISTS venue_show_counts CASCADE;

            CREATE TABLE IF NOT EXISTS venue_show_counts (
                id             integer NOT NULL,
                shows_at_venue bigint
            );
            CREATE UNIQUE INDEX IF NOT EXISTS venue_show_counts_id_uidx
                ON venue_show_counts (id);
        ");
    }

    protected override void Down()
    {
        throw new System.NotImplementedException();
    }
}
