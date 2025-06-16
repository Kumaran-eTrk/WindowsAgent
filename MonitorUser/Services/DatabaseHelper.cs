namespace MonitorUserStandalone.Services;

using System.Data.SQLite;
using Serilog;

public class DatabaseHelper : IDisposable
{
    private readonly string _connectionString;

    public DatabaseHelper(string dbFilePath)
    {
        dbFilePath = Environment.ExpandEnvironmentVariables(dbFilePath);
        if (!File.Exists(dbFilePath))
        {
            string[] _folders = dbFilePath.Split("\\");
            Directory.CreateDirectory(dbFilePath.Replace("\\" + _folders.Last(), ""));
            SQLiteConnection.CreateFile(dbFilePath);
        }
        _connectionString =
            $"Data Source={dbFilePath};Version=3;BusyTimeout=3000;Pooling=True;Max Pool Size=100;";
        InitializeDatabase();
    }

    public SQLiteConnection GetConnection()
    {
        return new SQLiteConnection(_connectionString);
    }

    private void InitializeDatabase()
    {
        using (var connection = GetConnection())
        {
            connection.Open();

            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    string createTableQuery =
                        @"
                CREATE TABLE IF NOT EXISTS ApplicationUsage (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    RequestBody TEXT,
                    Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
                )";
                    using (var command = new SQLiteCommand(createTableQuery, connection))
                    {
                        command.ExecuteNonQuery();
                        command.CommandTimeout = 2000;
                    }
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    Log.Error("Initializing Database error : " + ex.Message);
                    throw;
                }
            }
        }
    }

    public void ExecuteNonQuery(string query, Action<SQLiteCommand> parameterize)
    {
        using (var connection = new SQLiteConnection(_connectionString))
        {
            connection.Open();

            using (var transaction = connection.BeginTransaction())
            {
                using (var command = new SQLiteCommand(query, connection, transaction))
                {
                    parameterize(command);
                    command.ExecuteNonQuery();
                    transaction.Commit();
                }
            }
        }
    }

    public IEnumerable<SQLiteDataReader> ExecuteReader(
        string query,
        Action<SQLiteCommand> parameterize
    )
    {
        using (var connection = new SQLiteConnection(_connectionString))
        {
            connection.Open();

            using (var transaction = connection.BeginTransaction())
            {
                using (var command = new SQLiteCommand(query, connection))
                {
                    parameterize(command);
                    using (
                        var reader = command.ExecuteReader(
                            System.Data.CommandBehavior.CloseConnection
                        )
                    )
                    {
                        while (reader.Read())
                        {
                            yield return reader;
                        }
                    }
                }
            }
        }
    }

    public void RefreshDB()
    {
        using (var connection = new SQLiteConnection(_connectionString))
        {
            connection.Open();
            string refreshTableQuery = "VACUUM;";
            using (var command = new SQLiteCommand(refreshTableQuery, connection))
            {
                command.ExecuteNonQuery();
            }
        }
    }

    public void Dispose()
    {
        // Implement IDisposable pattern if needed
    }
}
