using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.SqlServer.Management.Smo;
using Npgsql;
using PgMigrationHelper.Helpers;
using PgMigrationHelper.Models;

namespace PgMigrationHelper
{
    internal class Program
    {
        private const string PostgreSqlConnectionStringName = "PostgreSqlConnectionString";

        private const string MsSqlConnectionStringName = "MsSqlConnectionString";

        private static ScriptingOptions _msSqlDumpScriptingOptions;

        private static IConfigurationRoot Configuration { get; set; }

        private static string ExportedDataFolderLocation => GetLocation("ExportedDataFolder");

        private static string Sqlserver2PgsqlUtilityFolderLocation => GetLocation("Sqlserver2pgsqlUtilityFolder");

        private static string PostgresDataImportQueriesGeneratorLocation => GetLocation("PostgresDataImportQueriesGenerator");

        private static string BcpQueryGeneratorLocation => GetLocation("BcpQueryGenerator");

        private static string DataTypesComparisonLocation => GetLocation("DataTypesComparison");

        private static string SequencesAndPkConstraintsGeneratorLocation => GetLocation("SequencesAndPkConstraintsGenerator");

        private static string MultiplePkConstraintsGeneratorLocation => GetLocation("MultiplePkConstraintsGenerator");

        private static string FkConstraintsGeneratorLocation => GetLocation("FKConstraintsGenerator");

        private static string DfConstraintsGeneratorLocation => GetLocation("DFConstraintsGenerator");

        private static string IndexesGeneratorLocation => GetLocation("IndexesGenerator");

        private static string PostgreSqlConnectionString => GetConnectionString(PostgreSqlConnectionStringName);

        private static string MsSqlConnectionString => GetConnectionString(MsSqlConnectionStringName);

        private static string PostgreSqlDatabaseName => Configuration["PostgreSqlDatabaseName"];

        private static string MsSqlDatabaseName => Configuration["MsSqlDatabaseName"];

        private static string PerlCommandForSchemaConverting => Configuration["PerlCommandForSchemaConverting"];

        private static IEnumerable<TableInfo> TablesToExclude => GetTablesToExclude();

        private static string TablesToExcludeAsString
        {
            get
            {
                var tablesToExcludeAsString = string.Join(", ", TablesToExclude.Select(x => $"'{x.SchemaName}.{x.TableName}'"));

                if (string.IsNullOrWhiteSpace(tablesToExcludeAsString))
                {
                    tablesToExcludeAsString = "''";
                }

                return tablesToExcludeAsString;
            }
        }

        private static void Main(string[] args)
        {
            IConfigurationBuilder builder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json");

            Configuration = builder.Build();

            IConfigurationSection dumpGenerationOptionsSection = Configuration.GetSection("MsSqlDumpGenerationOptions");

            _msSqlDumpScriptingOptions = new ScriptingOptions
            {
                ScriptData = bool.Parse(dumpGenerationOptionsSection["ScriptData"]),
                ScriptSchema = bool.Parse(dumpGenerationOptionsSection["ScriptSchema"]),
                ScriptDrops = bool.Parse(dumpGenerationOptionsSection["ScriptDrops"]),
                Indexes = bool.Parse(dumpGenerationOptionsSection["Indexes"]),
                IncludeHeaders = bool.Parse(dumpGenerationOptionsSection["IncludeHeaders"]),
                ClusteredIndexes = bool.Parse(dumpGenerationOptionsSection["ClusteredIndexes"]),
                DriAllConstraints = bool.Parse(dumpGenerationOptionsSection["DriAllConstraints"])
            };

            var actions = new List<UtilityAction>();

            if (args.Length > 0)
            {
                const string actionsArgumentName = "actions=";
                string argument = args.FirstOrDefault(x => x.StartsWith(actionsArgumentName));

                if (!string.IsNullOrWhiteSpace(argument))
                {
                    argument = argument.Substring(actionsArgumentName.Length);
                    var availableActions = (UtilityAction[])Enum.GetValues(typeof(UtilityAction));

                    actions = argument.Split(',').Select(x => availableActions.FirstOrDefault(action => action.ToString() == x)).ToList();
                }
            }

            if (args.Length == 0)
            {
                actions = RequestActions().ToList();
            }

            Run(actions);
        }

        private static IEnumerable<UtilityAction> RequestActions()
        {
            var availableActions = EnumHelpers.GetEnumItemsWithDescriptions<UtilityAction>(typeof(UtilityAction)).ToList();

            for (var i = 0; i < availableActions.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {availableActions[i].Value}");
            }

            Console.Write("\nEnter needed actions using comma as delimiter: ");
            string selectedActionsAsString = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(selectedActionsAsString))
            {
                return Enumerable.Empty<UtilityAction>();
            }

            try
            {
                var actionsIndexes = selectedActionsAsString.Replace(" ", "").Split(',').Select(x => int.Parse(x)).ToList();

                var selectedUtilityActions = availableActions.Where((x, index) => actionsIndexes.Contains(index + 1))
                    .Select(x => x.Key)
                    .ToList();

                return selectedUtilityActions;
            }
            catch (Exception e)
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Please enter required actions correctly!");
                Console.ForegroundColor = ConsoleColor.White;

                return RequestActions();
            }
        }

        private static void Run(List<UtilityAction> actions)
        {
            if (!actions.Any())
            {
                Console.WriteLine("No actions was selected");
                Console.ReadLine();
            }

            if (actions.Any(x => x == UtilityAction.Cleanup || x == UtilityAction.All))
            {
                Console.WriteLine("Cleaning up the needed environment");
                CleanupEnvironment();

                Console.WriteLine("Checking for PostgreSQL database existing. It will be created if not exists");
                CheckAndRepairEnvironment();
            }

            if (actions.Any(x => x == UtilityAction.ConvertSchema || x == UtilityAction.All))
            {
                Console.WriteLine("Generating MS SQL db dump");
                CreateMsSqlDumpFile();

                Console.WriteLine("Running schema convert query");
                ExecuteCmdCommandAndWaitForClose(PerlCommandForSchemaConverting, Sqlserver2PgsqlUtilityFolderLocation);

                RemoveUnnecessaryLinesInConvertedFiles();
            }

            if (actions.Any(x => x == UtilityAction.ImportSchema || x == UtilityAction.All))
            {
                Console.WriteLine("Import the schema using generated file");

                string beforeQuery = File.ReadAllText(
                    Path.Combine(Sqlserver2PgsqlUtilityFolderLocation, "tables-before.sql"),
                    Encoding.UTF8);

                ExecutePostgreSqlNonQuery(beforeQuery, PostgreSqlDatabaseName);
            }

            if (actions.Any(x => x == UtilityAction.GenerateCsv || x == UtilityAction.All))
            {
                Console.WriteLine("Read bcp queries generator");

                var bcpGenerationQuery = string.Format(
                    File.ReadAllText(BcpQueryGeneratorLocation),
                    Path.Combine(Environment.CurrentDirectory, ExportedDataFolderLocation),
                    MsSqlDatabaseName,
                    TablesToExcludeAsString);

                Console.WriteLine("Generating bcp queries");
                DataTable bcpQueriesDataTable = GetMsSqlQueryResult(bcpGenerationQuery);

                var bcpQueries = string.Join(
                    Environment.NewLine,
                    bcpQueriesDataTable.Rows.Cast<DataRow>().Select(x => x["Output"]).ToList());

                Console.WriteLine("Generating CSVs");
                ExecuteCmdCommandAndWaitForClose(bcpQueries, ExportedDataFolderLocation);

                Console.WriteLine("Removing null chars in CSVs");
                IEnumerable<string> files = new DirectoryInfo(ExportedDataFolderLocation).GetFiles("*.csv").Select(x => x.FullName);
                RemoveExtraDataInFiles(files);
            }

            if (actions.Any(x => x == UtilityAction.ImportData || x == UtilityAction.All))
            {
                Console.WriteLine("Generating PostgreSQL data import queries");
                string dataImportGeneratorQuery = File.ReadAllText(PostgresDataImportQueriesGeneratorLocation, Encoding.UTF8);
                string dataTypesComparisonSql = File.ReadAllText(DataTypesComparisonLocation, Encoding.UTF8);

                DataTable dataImportQueriesDataTable = GetMsSqlQueryResult(
                    dataTypesComparisonSql
                    + Environment.NewLine
                    + string.Format(dataImportGeneratorQuery, Path.Combine(Environment.CurrentDirectory, ExportedDataFolderLocation), TablesToExcludeAsString));

                var dataImportQueries = string.Join(
                    Environment.NewLine,
                    dataImportQueriesDataTable.Rows.Cast<DataRow>().Select(x => x["Output"]).ToList());

                Console.WriteLine("Importing data into PostgreSQL");
                ExecutePostgreSqlNonQuery(dataImportQueries, PostgreSqlDatabaseName);

                Console.WriteLine("Data was inserted into PostgreSQL");
            }

            if (actions.Any(x => x == UtilityAction.ImportConstraints || x == UtilityAction.All))
            {
                Console.WriteLine("Importing sequences and PK constraints");
                ImportConstraints(SequencesAndPkConstraintsGeneratorLocation);

                Console.WriteLine("Importing multiple PK constraints");
                ImportConstraints(MultiplePkConstraintsGeneratorLocation);

                Console.WriteLine("Importing FK constraints");
                ImportConstraints(FkConstraintsGeneratorLocation);

                Console.WriteLine("Importing DF constraints");
                ImportConstraints(DfConstraintsGeneratorLocation);

                Console.WriteLine("Importing indexes");
                ImportConstraints(IndexesGeneratorLocation);
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nComplete");
        }

        private static string GetMsSqlDump()
        {
            var dumpGenerator = new MsSqlDumpGenerator(MsSqlConnectionString, MsSqlDatabaseName);
            string dbDump = dumpGenerator.GetDump(_msSqlDumpScriptingOptions, TablesToExclude);

            return dbDump;
        }

        private static void ImportConstraints(string queryLocation)
        {
            string constraintsGenerationQuery = File.ReadAllText(queryLocation, Encoding.UTF8);
            constraintsGenerationQuery = string.Format(constraintsGenerationQuery, TablesToExcludeAsString);

            DataTable pgConstraintsQueriesDataTable = GetMsSqlQueryResult(constraintsGenerationQuery);

            var pgConstraintsQueries = string.Join(
                Environment.NewLine,
                pgConstraintsQueriesDataTable.Rows.Cast<DataRow>().Select(x => x["Output"]).ToList());

            if (!string.IsNullOrWhiteSpace(pgConstraintsQueries))
            {
                ExecutePostgreSqlNonQuery(pgConstraintsQueries, PostgreSqlDatabaseName);
            }
        }

        private static void CreateMsSqlDumpFile()
        {
            string dbDump = GetMsSqlDump();

            dbDump = RemoveWithStatements(dbDump);

            using (var sw = new StreamWriter(Path.Combine(Sqlserver2PgsqlUtilityFolderLocation, "script.sql"), false, Encoding.ASCII))
            {
                sw.Write(dbDump);
            }
        }

        private static string RemoveWithStatements(string dbDump)
        {
            return dbDump.Replace(@"WITH
(
DATA_COMPRESSION = PAGE
)","");
        }

        private static void RemoveUnnecessaryLinesInConvertedFiles()
        {
            string[] linesToReplace =
            {
                "\\set ON_ERROR_STOP",
                "\\set ECHO all"
            };

            string[] filesForCleaning =
            {
                "tables-before.sql",
                "tables-after.sql"
            };

            foreach (string fileForCleaning in filesForCleaning)
            {
                string combinedFilePath = Path.Combine(Sqlserver2PgsqlUtilityFolderLocation, fileForCleaning);
                string fileContent = File.ReadAllText(combinedFilePath);

                fileContent = linesToReplace.Aggregate(fileContent, (current, line) => current.Replace(line, ""));

                using (var sw = new StreamWriter(combinedFilePath, false, Encoding.UTF8))
                {
                    sw.Write(fileContent);
                }
            }
        }

        private static DataTable GetMsSqlQueryResult(string query)
        {
            var resultDataTable = new DataTable();

            using (var connection = new SqlConnection(MsSqlConnectionString))
            {
                connection.Open();

                using (var command = new SqlCommand(query, connection))
                {
                    using (var adapter = new SqlDataAdapter(command))
                    {
                        adapter.Fill(resultDataTable);
                    }
                }
            }

            return resultDataTable;
        }

        private static void ExecuteCmdCommandAndWaitForClose(string arguments, string startupPath = null)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd",
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            if (!string.IsNullOrWhiteSpace(startupPath))
            {
                process.StandardInput.WriteLine("cd " + startupPath);
            }

            process.StandardInput.WriteLine(arguments);
            process.WaitForExit(5 * 1000);
        }

        private static void CheckAndRepairEnvironment()
        {
            RecreatePostgreSqlDatabase();

            if (!Directory.Exists(ExportedDataFolderLocation))
            {
                Directory.CreateDirectory(ExportedDataFolderLocation);
            }
        }

        private static void RecreatePostgreSqlDatabase()
        {
            ExecutePostgreSqlNonQuery($"DROP DATABASE IF EXISTS \"{PostgreSqlDatabaseName}\";");
            ExecutePostgreSqlNonQuery($"CREATE DATABASE \"{PostgreSqlDatabaseName}\";");
        }

        private static string GetLocation(string key)
        {
            return Configuration.GetSection("Locations")[key];
        }

        private static void CleanupEnvironment()
        {
            // Removing exported csv files
            if (Directory.Exists(ExportedDataFolderLocation))
            {
                string[] dataFolderFiles = Directory.GetFiles(ExportedDataFolderLocation, "*.csv");

                foreach (string file in dataFolderFiles)
                {
                    File.Delete(file);
                }
            }

            // Removing exported sql-scripts
            string[] sqlScriptFiles = Directory.GetFiles(Sqlserver2PgsqlUtilityFolderLocation, "*.sql");

            foreach (string file in sqlScriptFiles)
            {
                File.Delete(file);
            }
        }

        private static void ExecutePostgreSqlNonQuery(string query, string dbName = "postgres")
        {
            using (var connection = new NpgsqlConnection(PostgreSqlConnectionString))
            {
                connection.Open();
                connection.ChangeDatabase(dbName);

                using (var cmd = new NpgsqlCommand(query, connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static string GetConnectionString(string connectionStringName)
        {
            return Configuration.GetConnectionString(connectionStringName);
        }

        private static void RemoveExtraDataInFiles(IEnumerable<string> files)
        {
            foreach (string fileInfo in files)
            {
                string fileContent = File.ReadAllText(fileInfo);

                fileContent = RemoveNullCharsInFile(fileContent);
                fileContent = RemoveEmptyLinesInFile(fileContent);

                using (var sw = new StreamWriter(fileInfo, false, new UTF8Encoding(false)))
                {
                    sw.Write(fileContent);
                }
            }
        }

        private static string RemoveNullCharsInFile(string content)
        {
            return content.Replace(((char)0).ToString(), "");
        }

        private static string RemoveEmptyLinesInFile(string content)
        {
            var lines = content.Replace("\r", "").Split('\n').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

            return string.Join(Environment.NewLine, lines);
        }

        private static IEnumerable<TableInfo> GetTablesToExclude()
        {
            IEnumerable<IConfigurationSection> itemsToExclude = Configuration.GetSection("Exclude").GetChildren();
            IConfigurationSection tablesSection = itemsToExclude.FirstOrDefault(x => x.Key == "Tables");

            IEnumerable<TableInfo> tables = tablesSection.GetChildren()
                .SelectMany(
                    x => x.GetSection("Tables")
                        .GetChildren()
                        .Select(
                            y => new TableInfo
                            {
                                SchemaName = x["Schema"],
                                TableName = y.Value
                            }));

            return tables;
        }
    }
}
