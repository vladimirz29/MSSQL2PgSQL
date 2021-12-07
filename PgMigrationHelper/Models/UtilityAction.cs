using System.ComponentModel;

namespace PgMigrationHelper.Models
{
    internal enum UtilityAction
    {
        [Description("Cleanup and repair environment. Create PostgreSQL database")]
        Cleanup,

        [Description("Convert schema")]
        ConvertSchema,

        [Description("Import schema")]
        ImportSchema,

        [Description("Export CSV files contains data of MS SQL database")]
        GenerateCsv,

        [Description("Import data")]
        ImportData,

        [Description("Import constraints and indexes")]
        ImportConstraints,

        [Description("All")]
        All
    }
}
