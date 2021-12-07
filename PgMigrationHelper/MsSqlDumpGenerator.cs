using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using PgMigrationHelper.Models;

namespace PgMigrationHelper
{
    public class MsSqlDumpGenerator
    {
        private readonly string _connectionString;
        private readonly string _databaseName;

        public MsSqlDumpGenerator(string connectionString, string databaseName)
        {
            _connectionString = connectionString;
            _databaseName = databaseName;
        }

        public string GetDump(ScriptingOptions options, IEnumerable<TableInfo> tablesToExclude = null)
        {
            var script = new StringBuilder();

            var server = new Server(new ServerConnection(new SqlConnection(_connectionString)));
            Database database = server.Databases[_databaseName];

            IEnumerable<Table> tables = ExcludeTables(database.Tables, tablesToExclude);

            if (!tables.Any())
            {
                throw new Exception("There are no tables to get dump");
            }

            // export tables schema without indexes
            bool isIndexesExportEnabled = options.Indexes;
            options.Indexes = false;

            foreach (Table table in tables)
            {
                foreach (string statement in table.EnumScript(options))
                {
                    script.Append(statement);
                    script.Append(Environment.NewLine);
                }
            }

            // append indexes
            if (isIndexesExportEnabled)
            {
                foreach (Table table in tables)
                {
                    foreach (string statement in table.EnumScript(
                        new ScriptingOptions
                        {
                            Indexes = true
                        }))
                    {
                        script.Append(statement);
                        script.Append(Environment.NewLine);
                    }
                }
            }

            return script.ToString();
        }

        private IEnumerable<Table> ExcludeTables(TableCollection tables, IEnumerable<TableInfo> tablesToExclude)
        {
            return tables.Cast<Table>()
                .Where(
                    table => (tablesToExclude == null)
                        || !tablesToExclude.Any(x => (x.SchemaName == table.Schema) && (x.TableName == table.Name)));
        }
    }
}
