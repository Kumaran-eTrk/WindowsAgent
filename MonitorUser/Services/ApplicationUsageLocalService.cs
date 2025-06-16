using System;
using System.Collections.Generic;
using System.Data.SQLite;
using MonitorUserStandalone.Entity;
using Newtonsoft.Json;
using Serilog;

namespace MonitorUserStandalone.Services;

public class ApplicationUsageLocalService : IApplicationUsageLocalService
{
    private readonly DatabaseHelper _dbHelper;

    public ApplicationUsageLocalService(DatabaseHelper dbHelper)
    {
        _dbHelper = dbHelper;
    }

    public void StoreRequestBody(UserActivity requestBody)
    {
        using (var connection = _dbHelper.GetConnection())
        {
            connection.Open();

            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    string requestHash = JsonConvert.SerializeObject(requestBody);
                    string query =
                        "INSERT INTO ApplicationUsage (RequestBody) VALUES (@requestBody)";
                    // _dbHelper.ExecuteNonQuery(
                    //     query,
                    //     command =>
                    //     {
                    //         command.Parameters.AddWithValue("@requestBody", requestHash);

                    //     }

                    // );
                    using (var command = new SQLiteCommand(query, connection, transaction))
                    {
                        command.Parameters.AddWithValue("@requestBody", requestHash);
                        command.ExecuteNonQuery();
                        command.CommandTimeout = 2000;
                    }
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    Log.Error("Inserting the data error : " + ex.Message);
                    Log.Error("Inserting the data error : " + ex.StackTrace);
                }
            }
        }
    }

    public ApplicationUsageLocal GetRequestById(int id)
    {
        string query = "SELECT * FROM ApplicationUsage WHERE Id = @id";
        var reader = _dbHelper
            .ExecuteReader(
                query,
                command =>
                {
                    command.Parameters.AddWithValue("@id", id);
                }
            )
            .FirstOrDefault();

        if (reader != null)
        {
            return new ApplicationUsageLocal
            {
                Id = Convert.ToInt32(reader["Id"]),
                RequestBody = reader["RequestBody"].ToString(),
                Timestamp = Convert.ToDateTime(reader["Timestamp"])
            };
        }

        return null;
    }

    public IEnumerable<ApplicationUsageLocal> GetAllRequests()
    {
        string query = "SELECT * FROM ApplicationUsage";
        var readers = _dbHelper.ExecuteReader(query, command => { });

        foreach (var reader in readers)
        {
            yield return new ApplicationUsageLocal
            {
                Id = Convert.ToInt32(reader["Id"]),
                RequestBody = reader["RequestBody"].ToString(),
                Timestamp = Convert.ToDateTime(reader["Timestamp"])
            };
        }
    }

    public void DeleteRequest(int id)
    {
        try
        {
            string query = "DELETE FROM ApplicationUsage WHERE Id = @id";
            _dbHelper.ExecuteNonQuery(
                query,
                command =>
                {
                    command.Parameters.AddWithValue("@id", id);
                }
            );
        }
        catch (Exception ex)
        {
            Log.Error("DeleteRequest by Id : " + ex.Message);
            Log.Error("DeleteRequest by Id : " + ex.InnerException);
        }
    }

    public void DeleteAll()
    {
        try
        {
            string query = "DELETE FROM ApplicationUsage";
            _dbHelper.ExecuteNonQuery(query, command => { });
        }
        catch (Exception ex)
        {
            Log.Error("DeleteRequest by Id : " + ex.Message);
            Log.Error("DeleteRequest by Id : " + ex.InnerException);
        }
    }
}
