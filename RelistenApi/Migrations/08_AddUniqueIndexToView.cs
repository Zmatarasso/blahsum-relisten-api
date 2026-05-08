using SimpleMigrations;

namespace Migrations;

[Migration(8, "Add unique index to source_track_plays_by_day_6mo")]
public class AddUniqueIndexToView: Migration {
    public AddUniqueIndexToView()
    {
        this.UseTransaction = false;
    }

    protected override void Up()
    {
        // blahsum-relisten: upstream migration 8 assumed `source_track_plays_by_hour_48h`
        // and `source_track_plays_by_day_6mo` already existed (they live in upstream's
        // production DB seed but no migration creates them). On an empty DB we have to
        // create them here. Shape derived from the equivalent `source_track_plays_hourly`
        // / `source_track_plays_daily` continuous aggregates in
        // Migrations/timescaledb_migration.sql, plus the column references in
        // ScheduledService.cs / docs/popularity-trending-design.md. These are plain
        // materialized views (refreshed by ScheduledService Hangfire jobs), not
        // TimescaleDB continuous aggregates.
        Execute(@"
            CREATE MATERIALIZED VIEW IF NOT EXISTS source_track_plays_by_hour_48h AS
            SELECT
                date_trunc('hour', p.created_at) AS play_hour,
                p.source_track_uuid,
                a.uuid AS artist_uuid,
                s.uuid AS source_uuid,
                sh.uuid AS show_uuid,
                count(*) AS plays,
                sum(coalesce(t.duration, 0)) AS total_track_seconds
            FROM source_track_plays p
                JOIN source_tracks t ON p.source_track_uuid = t.uuid
                JOIN artists a ON a.id = t.artist_id
                JOIN sources s ON s.id = t.source_id
                JOIN shows sh ON sh.id = s.show_id
            WHERE p.created_at >= now() - interval '48 hours'
            GROUP BY 1, 2, 3, 4, 5
            WITH NO DATA;
        ");

        Execute(@"
            CREATE MATERIALIZED VIEW IF NOT EXISTS source_track_plays_by_day_6mo AS
            SELECT
                date_trunc('day', p.created_at) AS play_day,
                p.source_track_uuid,
                a.uuid AS artist_uuid,
                s.uuid AS source_uuid,
                sh.uuid AS show_uuid,
                count(*) AS plays,
                sum(coalesce(t.duration, 0)) AS total_track_seconds
            FROM source_track_plays p
                JOIN source_tracks t ON p.source_track_uuid = t.uuid
                JOIN artists a ON a.id = t.artist_id
                JOIN sources s ON s.id = t.source_id
                JOIN shows sh ON sh.id = s.show_id
            WHERE p.created_at >= now() - interval '6 months'
            GROUP BY 1, 2, 3, 4, 5
            WITH NO DATA;
        ");

        // In practice, there won't be duplicates but we don't need to enforce at the DB level
        Execute(@"
            CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ""source_track_plays_by_hour_48h_play_hour_source_track_uuid_idx"" ON ""public"".""source_track_plays_by_hour_48h""(""play_hour"",""source_track_uuid"");
        ");
        Execute(@"
            CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ""source_track_plays_by_day_6mo_source_track_uuid_play_day_idx"" ON ""source_track_plays_by_day_6mo""(""source_track_uuid"",""play_day"");
        ");
    }

    protected override void Down()
    {
        throw new System.NotImplementedException();
    }
}
