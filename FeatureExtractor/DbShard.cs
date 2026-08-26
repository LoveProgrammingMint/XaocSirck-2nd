using Microsoft.Data.Sqlite;

namespace FeatureExtractor;

internal static class DbShard
{
    private const String SelectAllSql = """
        SELECT sha256, file_path, file_size, rbgi, pessi, bevi, efg, idg, fcg, dis, cms, extracted_at
        FROM pe_features
        """;

    private const String UpsertSql = """
        INSERT OR REPLACE INTO pe_features
            (sha256, file_path, file_size, rbgi, pessi, bevi, efg, idg, fcg, dis, cms, extracted_at)
        VALUES
            (@sha, @path, @size, @rbgi, @pessi, @bevi, @efg, @idg, @fcg, @dis, @cms, @ts)
        """;

    public static Int32 MergeIntoShards(String[] workerDbs, String dataDir, Int32 capacity)
    {
        Directory.CreateDirectory(dataDir);
        Int32 merged = 0;

        String currentShard = FindAppendableShard(dataDir, capacity, out Int32 currentCount);
        SqliteConnection dstConn = OpenShard(currentShard);
        SqliteTransaction tx = dstConn.BeginTransaction();
        SqliteCommand cmd = BuildInsertCmd(dstConn, tx);

        try
        {
            foreach (String workerDb in workerDbs)
            {
                if (!File.Exists(workerDb)) continue;
                using SqliteConnection srcConn = new($"Data Source={workerDb};Mode=ReadOnly");
                srcConn.Open();
                using SqliteCommand srcCmd = srcConn.CreateCommand();
                srcCmd.CommandText = SelectAllSql;
                using SqliteDataReader r = srcCmd.ExecuteReader();

                while (r.Read())
                {
                    if (currentCount >= capacity)
                    {
                        tx.Commit();
                        cmd.Dispose();
                        tx.Dispose();
                        dstConn.Dispose();

                        currentShard = NextShardPath(dataDir);
                        dstConn = OpenShard(currentShard);
                        tx = dstConn.BeginTransaction();
                        cmd = BuildInsertCmd(dstConn, tx);
                        currentCount = 0;
                    }

                    BindReader(cmd, r);
                    cmd.ExecuteNonQuery();
                    currentCount++;
                    merged++;
                }
            }

            tx.Commit();
            cmd.Dispose();
            tx.Dispose();
            dstConn.Dispose();
        }
        catch
        {
            try { tx.Rollback(); } catch { }
            throw;
        }
        return merged;
    }

    public static Int32 CountRecords(String dbPath)
    {
        if (!File.Exists(dbPath)) return 0;
        using SqliteConnection conn = new($"Data Source={dbPath};Mode=ReadOnly");
        conn.Open();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM pe_features";
        Object? v = cmd.ExecuteScalar();
        return v is Int64 i ? (Int32)i : 0;
    }

    private static String FindAppendableShard(String dataDir, Int32 capacity, out Int32 currentCount)
    {
        String[] existing = Directory.EnumerateFiles(dataDir, "*DATA.db", SearchOption.TopDirectoryOnly)
            .Where(f => ShardIndex(f) > 0)
            .OrderBy(f => ShardIndex(f))
            .ToArray();

        if (existing.Length > 0)
        {
            String last = existing[^1];
            Int32 cnt = CountRecords(last);
            if (cnt < capacity)
            {
                currentCount = cnt;
                return last;
            }
        }

        String path = NextShardPath(dataDir);
        EnsureShard(path);
        currentCount = 0;
        return path;
    }

    private static String NextShardPath(String dataDir)
    {
        Int32 max = 0;
        foreach (String f in Directory.EnumerateFiles(dataDir, "*DATA.db", SearchOption.TopDirectoryOnly))
        {
            Int32 idx = ShardIndex(f);
            if (idx > max) max = idx;
        }
        return Path.Combine(dataDir, $"{max + 1:D3}DATA.db");
    }

    private static Int32 ShardIndex(String path)
    {
        String name = Path.GetFileNameWithoutExtension(path);
        if (name.Length < 7 || !name.EndsWith("DATA", StringComparison.Ordinal)) return 0;
        String prefix = name[..^4];
        return Int32.TryParse(prefix, out Int32 idx) ? idx : 0;
    }

    private static SqliteConnection OpenShard(String path)
    {
        EnsureShard(path);
        SqliteConnection conn = new($"Data Source={path}");
        conn.Open();
        return conn;
    }

    private static void EnsureShard(String path)
    {
        using SqliteConnection conn = new($"Data Source={path}");
        conn.Open();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = Worker.CreateTableSql;
        cmd.ExecuteNonQuery();
    }

    private static readonly String[] ParamNames =
        ["@sha", "@path", "@size", "@rbgi", "@pessi", "@bevi", "@efg", "@idg", "@fcg", "@dis", "@cms", "@ts"];

    private static SqliteCommand BuildInsertCmd(SqliteConnection conn, SqliteTransaction tx)
    {
        SqliteCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = UpsertSql;
        foreach (String name in ParamNames)
            cmd.Parameters.Add(new SqliteParameter(name, null));
        return cmd;
    }

    private static void BindReader(SqliteCommand cmd, SqliteDataReader r)
    {
        cmd.Parameters[0].Value = r.GetString(0);
        cmd.Parameters[1].Value = r.GetString(1);
        cmd.Parameters[2].Value = r.GetInt32(2);
        cmd.Parameters[3].Value = r.IsDBNull(3) ? DBNull.Value : r.GetValue(3);
        cmd.Parameters[4].Value = r.IsDBNull(4) ? DBNull.Value : r.GetValue(4);
        cmd.Parameters[5].Value = r.IsDBNull(5) ? DBNull.Value : r.GetValue(5);
        cmd.Parameters[6].Value = r.IsDBNull(6) ? DBNull.Value : r.GetValue(6);
        cmd.Parameters[7].Value = r.IsDBNull(7) ? DBNull.Value : r.GetValue(7);
        cmd.Parameters[8].Value = r.IsDBNull(8) ? DBNull.Value : r.GetValue(8);
        cmd.Parameters[9].Value = r.IsDBNull(9) ? DBNull.Value : r.GetValue(9);
        cmd.Parameters[10].Value = r.IsDBNull(10) ? DBNull.Value : r.GetValue(10);
        cmd.Parameters[11].Value = r.GetString(11);
    }
}
