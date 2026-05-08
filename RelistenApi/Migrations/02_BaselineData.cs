using SimpleMigrations;

namespace Migrations
{
    // blahsum-relisten: replaced with a no-op.
    // Upstream migration 2 inserts Relisten.net's hardcoded artist roster
    // (Phish, Grateful Dead, Goose, etc.). For our own catalog we want this
    // table empty after migration. The migration version (2) is preserved so
    // the migration history aligns with upstream and 03+ continue to apply.
    [Migration(2, "Baseline Artist Data (no-op for blahsum-relisten)")]
    public class BaselineData : Migration
    {
        protected override void Up()
        {
        }
    }
}
