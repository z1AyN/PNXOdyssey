using System.Text.Json;
using Microsoft.Data.Sqlite;
using Pnx.Odyssey.Core;

namespace Pnx.Odyssey.Server;

public sealed class StateStore
{
    private readonly string _path;

    public StateStore(string path)
    {
        _path = path;
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using SqliteConnection connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS state (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                json TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    public HostArchive Load()
    {
        using SqliteConnection connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT json FROM state WHERE id = 1";
        object? value = command.ExecuteScalar();
        if (value is not string json || json.Length == 0)
            return new HostArchive();

        return JsonSerializer.Deserialize<HostArchive>(json, ProtocolJson.Options) ?? new HostArchive();
    }

    public void Save(HostArchive archive)
    {
        string json = JsonSerializer.Serialize(archive, ProtocolJson.Options);
        using SqliteConnection connection = Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO state (id, json) VALUES (1, $json)
            ON CONFLICT(id) DO UPDATE SET json = excluded.json;
            """;
        command.Parameters.AddWithValue("$json", json);
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection($"Data Source={_path};Mode=ReadWriteCreate;Cache=Shared");
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL;";
        pragma.ExecuteNonQuery();
        return connection;
    }
}
