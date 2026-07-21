using DASA.Host.Models;
using Microsoft.Data.Sqlite;

namespace DASA.Host.Services;

public sealed class HistoryStore
{
    private readonly string _dbPath;
    private readonly object _sync = new();

    public HistoryStore(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _dbPath = Path.Combine(dataDirectory, "history.db");
        Initialize();
    }

    private string ConnectionString => $"Data Source={_dbPath}";

    private void Initialize()
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            CREATE TABLE IF NOT EXISTS history (
                id TEXT PRIMARY KEY,
                original_path TEXT NOT NULL,
                destination_path TEXT NOT NULL,
                file_name TEXT NOT NULL,
                category TEXT NOT NULL,
                source TEXT NOT NULL,
                confidence REAL,
                undo_token TEXT,
                quarantined INTEGER NOT NULL DEFAULT 0,
                timestamp TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS quarantine_events (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                file_path TEXT NOT NULL,
                quarantine_path TEXT NOT NULL,
                detail TEXT NOT NULL,
                timestamp TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS undo_tokens (
                token TEXT PRIMARY KEY,
                original_path TEXT NOT NULL,
                destination_path TEXT NOT NULL,
                created_at TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public void Add(FileProcessedPayload item)
    {
        lock (_sync)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                """
                INSERT OR REPLACE INTO history
                (id, original_path, destination_path, file_name, category, source, confidence, undo_token, quarantined, timestamp)
                VALUES ($id, $o, $d, $n, $c, $s, $conf, $u, $q, $t);
                """;
            cmd.Parameters.AddWithValue("$id", item.Id);
            cmd.Parameters.AddWithValue("$o", item.OriginalPath);
            cmd.Parameters.AddWithValue("$d", item.DestinationPath);
            cmd.Parameters.AddWithValue("$n", item.FileName);
            cmd.Parameters.AddWithValue("$c", item.Category);
            cmd.Parameters.AddWithValue("$s", item.Source);
            cmd.Parameters.AddWithValue("$conf", (object?)item.Confidence ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$u", (object?)item.UndoToken ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$q", item.Quarantined ? 1 : 0);
            cmd.Parameters.AddWithValue("$t", item.Timestamp.ToString("O"));
            cmd.ExecuteNonQuery();

            if (!string.IsNullOrWhiteSpace(item.UndoToken) && !item.Quarantined)
            {
                using var undo = conn.CreateCommand();
                undo.CommandText =
                    """
                    INSERT OR REPLACE INTO undo_tokens (token, original_path, destination_path, created_at)
                    VALUES ($t, $o, $d, $c);
                    """;
                undo.Parameters.AddWithValue("$t", item.UndoToken);
                undo.Parameters.AddWithValue("$o", item.OriginalPath);
                undo.Parameters.AddWithValue("$d", item.DestinationPath);
                undo.Parameters.AddWithValue("$c", DateTimeOffset.UtcNow.ToString("O"));
                undo.ExecuteNonQuery();
            }
        }
    }

    public void AddQuarantine(MalwareDetectedPayload payload)
    {
        lock (_sync)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO quarantine_events (file_path, quarantine_path, detail, timestamp)
                VALUES ($f, $q, $d, $t);
                """;
            cmd.Parameters.AddWithValue("$f", payload.FilePath);
            cmd.Parameters.AddWithValue("$q", payload.QuarantinePath);
            cmd.Parameters.AddWithValue("$d", payload.Detail);
            cmd.Parameters.AddWithValue("$t", payload.Timestamp.ToString("O"));
            cmd.ExecuteNonQuery();
        }
    }

    public List<FileProcessedPayload> GetRecent(int limit = 100) => GetPage(0, limit);

    public int GetCount()
    {
        lock (_sync)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM history;";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    public List<FileProcessedPayload> GetPage(int offset, int limit)
    {
        lock (_sync)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                """
                SELECT id, original_path, destination_path, file_name, category, source, confidence, undo_token, quarantined, timestamp
                FROM history
                ORDER BY timestamp DESC
                LIMIT $limit OFFSET $offset;
                """;
            cmd.Parameters.AddWithValue("$limit", limit);
            cmd.Parameters.AddWithValue("$offset", offset);

            var list = new List<FileProcessedPayload>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(ReadHistoryRow(reader));
            }

            return list;
        }
    }

    private static FileProcessedPayload ReadHistoryRow(SqliteDataReader reader)
    {
        return new FileProcessedPayload
        {
            Id = reader.GetString(0),
            OriginalPath = reader.GetString(1),
            DestinationPath = reader.GetString(2),
            FileName = reader.GetString(3),
            Category = reader.GetString(4),
            Source = reader.GetString(5),
            Confidence = reader.IsDBNull(6) ? null : reader.GetDouble(6),
            UndoToken = reader.IsDBNull(7) ? null : reader.GetString(7),
            Quarantined = reader.GetInt32(8) == 1,
            Timestamp = DateTimeOffset.Parse(reader.GetString(9))
        };
    }

    public List<MalwareDetectedPayload> GetQuarantineEvents(int limit = 50)
    {
        lock (_sync)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                """
                SELECT file_path, quarantine_path, detail, timestamp
                FROM quarantine_events
                ORDER BY id DESC
                LIMIT $limit;
                """;
            cmd.Parameters.AddWithValue("$limit", limit);

            var list = new List<MalwareDetectedPayload>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new MalwareDetectedPayload
                {
                    FilePath = reader.GetString(0),
                    QuarantinePath = reader.GetString(1),
                    Detail = reader.GetString(2),
                    Timestamp = DateTimeOffset.Parse(reader.GetString(3))
                });
            }

            return list;
        }
    }

    public void ClearActivity()
    {
        lock (_sync)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "DELETE FROM history;";
                cmd.ExecuteNonQuery();
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "DELETE FROM undo_tokens;";
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }
    }

    public (string OriginalPath, string DestinationPath)? ConsumeUndoToken(string token)
    {
        lock (_sync)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();

            using var select = conn.CreateCommand();
            select.Transaction = tx;
            select.CommandText =
                "SELECT original_path, destination_path FROM undo_tokens WHERE token = $t LIMIT 1;";
            select.Parameters.AddWithValue("$t", token);

            using var reader = select.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            var original = reader.GetString(0);
            var destination = reader.GetString(1);
            reader.Close();

            using var delete = conn.CreateCommand();
            delete.Transaction = tx;
            delete.CommandText = "DELETE FROM undo_tokens WHERE token = $t;";
            delete.Parameters.AddWithValue("$t", token);
            delete.ExecuteNonQuery();

            tx.Commit();
            return (original, destination);
        }
    }
}
