using System.Data;
using Microsoft.Data.SqlClient;

namespace MetroTechAssistant.Base;

/// <summary>Shared ADO.NET execution for every repository (stored procedures only).</summary>
public abstract class BaseRepository
{
    private readonly string _connectionString;

    protected BaseRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing in appsettings.json.");
    }

    /// <summary>Runs a stored procedure and returns the rows it selects.</summary>
    protected async Task<DataTable> ExecuteDataTableAsync(string storedProcedure, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = CreateCommand(connection, storedProcedure, parameters);

        await connection.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();

        var table = new DataTable();
        table.Load(reader);
        return table;
    }

    /// <summary>Runs a stored procedure that changes data (insert / update / delete).</summary>
    protected async Task<int> ExecuteNonQueryAsync(string storedProcedure, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = CreateCommand(connection, storedProcedure, parameters);

        await connection.OpenAsync();
        return await command.ExecuteNonQueryAsync();
    }

    private static SqlCommand CreateCommand(SqlConnection connection, string storedProcedure, SqlParameter[] parameters)
    {
        var command = new SqlCommand(storedProcedure, connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        if (parameters is { Length: > 0 })
            command.Parameters.AddRange(parameters);

        return command;
    }
}
