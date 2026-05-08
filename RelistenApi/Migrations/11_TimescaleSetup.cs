using SimpleMigrations;

namespace Migrations;

[Migration(11, "Convert source_track_plays to a TimescaleDB hypertable and create hourly/daily continuous aggregates")]
public class TimescaleSetup : Migration
{
    public TimescaleSetup()
    {
        // Continuous aggregates and CONCURRENTLY index creation cannot run inside
        // a transaction.
        this.UseTransaction = false;
    }

    protected override void Up()
    {
        // blahsum-relisten: upstream production set up these objects via the loose
        // Migrations/timescaledb_migration.sql script run by hand at some point.
        // Recent controllers (ArtistService.cs, etc.) query
        // source_track_plays_hourly and source_track_plays_daily. On an empty DB
        // we have to create them explicitly. This migration is the empty-DB
        // equivalent of timescaledb_migration.sql, simplified because we have
        // nothing to backfill.

        Execute(@"CREATE EXTENSION IF NOT EXISTS timescaledb;");

        // Convert source_track_plays into a hypertable. migrate_data => true
        // is unnecessary on empty data but harmless if ever non-empty.
        Execute(@"
            SELECT create_hypertable(
                'source_track_plays',
                'created_at',
                chunk_time_interval => interval '1 day',
                if_not_exists => true,
                migrate_data => true
            );
        ");

        Execute(@"
            CREATE MATERIALIZED VIEW IF NOT EXISTS source_track_plays_hourly
            WITH (timescaledb.continuous) AS
            SELECT
                time_bucket('1 hour', p.created_at) AS play_hour,
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
            GROUP BY 1, 2, 3, 4, 5
            WITH NO DATA;
        ");

        Execute(@"
            CREATE INDEX IF NOT EXISTS source_track_plays_hourly_play_hour_artist_idx
                ON source_track_plays_hourly (play_hour DESC, artist_uuid);
        ");
        Execute(@"
            CREATE INDEX IF NOT EXISTS source_track_plays_hourly_play_hour_show_idx
                ON source_track_plays_hourly (play_hour DESC, show_uuid);
        ");

        Execute(@"
            CREATE MATERIALIZED VIEW IF NOT EXISTS source_track_plays_daily
            WITH (timescaledb.continuous) AS
            SELECT
                time_bucket('1 day', p.created_at) AS play_day,
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
            GROUP BY 1, 2, 3, 4, 5
            WITH NO DATA;
        ");

        Execute(@"
            CREATE INDEX IF NOT EXISTS source_track_plays_daily_play_day_artist_idx
                ON source_track_plays_daily (play_day DESC, artist_uuid);
        ");
        Execute(@"
            CREATE INDEX IF NOT EXISTS source_track_plays_daily_play_day_show_idx
                ON source_track_plays_daily (play_day DESC, show_uuid);
        ");

        // Continuous-aggregate refresh policies. Use a DO block so re-running the
        // migration doesn't error if a policy was added by hand.
        Execute(@"
            DO $$ BEGIN
                PERFORM add_continuous_aggregate_policy(
                    'source_track_plays_hourly',
                    start_offset => interval '6 hours',
                    end_offset   => interval '1 hour',
                    schedule_interval => interval '1 hour'
                );
            EXCEPTION WHEN duplicate_object THEN
                NULL;
            END $$;
        ");

        Execute(@"
            DO $$ BEGIN
                PERFORM add_continuous_aggregate_policy(
                    'source_track_plays_daily',
                    start_offset => interval '3 days',
                    end_offset   => interval '1 day',
                    schedule_interval => interval '1 day'
                );
            EXCEPTION WHEN duplicate_object THEN
                NULL;
            END $$;
        ");
    }

    protected override void Down()
    {
        throw new System.NotImplementedException();
    }
}
