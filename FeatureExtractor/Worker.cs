using System.Security.Cryptography;

using Microsoft.Data.Sqlite;

using OverThink_ICeZeRoX.Feature;

namespace FeatureExtractor;

internal static class Worker
{
    public const String CreateTableSql = """
        CREATE TABLE IF NOT EXISTS pe_features (
            sha256 TEXT PRIMARY KEY,
            file_path TEXT NOT NULL,
            file_size INTEGER NOT NULL,
            rbgi BLOB, pessi BLOB, bevi BLOB,
            efg BLOB, idg BLOB, fcg BLOB,
            dis BLOB, cms BLOB,
            extracted_at TEXT NOT NULL
        );
        """;

    public static Int32 Run(ReadOnlySpan<String> args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("worker: missing args");
            return 1;
        }
        String dbPath = args[0];
        String[] files = args[1..].ToArray();
        if (files.Length == 0) return 0;
        return RunCore(dbPath, files);
    }

    public static Int32 RunFileList(ReadOnlySpan<String> args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("worker-filelist: missing args");
            return 1;
        }
        String dbPath = args[0];
        String listPath = args[1];
        if (!File.Exists(listPath))
        {
            Console.Error.WriteLine($"worker-filelist: list not found: {listPath}");
            return 1;
        }
        String[] files = File.ReadAllLines(listPath);
        if (files.Length == 0) return 0;
        return RunCore(dbPath, files);
    }

    private static Int32 RunCore(String dbPath, String[] files)
    {
        EnsureDatabase(dbPath);
        Int32 ok = 0;
        foreach (String f in files)
        {
            if (ExtractOne(f, dbPath))
                ok++;
            Console.WriteLine($"PROGRESS:{ok}/{files.Length}:{Path.GetFileName(f)}");
        }
        return 0;
    }

    private static void EnsureDatabase(String dbPath)
    {
        using SqliteConnection conn = new($"Data Source={dbPath}");
        conn.Open();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = CreateTableSql;
        cmd.ExecuteNonQuery();
    }

    private static Boolean ExtractOne(String pePath, String dbPath)
    {
        try
        {
            Byte[] raw = File.ReadAllBytes(pePath);
            String sha256 = Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant();

            String tempDir = Path.Combine(Path.GetTempPath(), "otx_w", sha256);
            Directory.CreateDirectory(tempDir);

            IReadOnlyList<Entry.GenerationResult> results = Entry.GenerateAll(pePath, tempDir);

            Dictionary<String, Byte[]> blobs = new(8);
            foreach (Entry.GenerationResult r in results)
                blobs[r.Tag] = File.ReadAllBytes(r.Path);

            try { Directory.Delete(tempDir, recursive: true); } catch { }

            Upsert(dbPath, sha256, pePath, raw.Length, blobs);
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"fail {pePath}: {ex.Message}");
            return false;
        }
    }

    private static void Upsert(String dbPath, String sha256, String filePath, Int32 fileSize, Dictionary<String, Byte[]> blobs)
    {
        using SqliteConnection conn = new($"Data Source={dbPath}");
        conn.Open();
        using SqliteTransaction tx = conn.BeginTransaction();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT OR REPLACE INTO pe_features
                (sha256, file_path, file_size, rbgi, pessi, bevi, efg, idg, fcg, dis, cms, extracted_at)
            VALUES
                (@sha, @path, @size, @rbgi, @pessi, @bevi, @efg, @idg, @fcg, @dis, @cms, @ts)
            """;
        cmd.Parameters.AddWithValue("@sha", sha256);
        cmd.Parameters.AddWithValue("@path", filePath);
        cmd.Parameters.AddWithValue("@size", fileSize);
        cmd.Parameters.AddWithValue("@ts", DateTime.UtcNow.ToString("O"));
        AddBlob(cmd, "@rbgi", blobs, "RBGI");
        AddBlob(cmd, "@pessi", blobs, "PESSI");
        AddBlob(cmd, "@bevi", blobs, "BEVI");
        AddBlob(cmd, "@efg", blobs, "EFG");
        AddBlob(cmd, "@idg", blobs, "IDG");
        AddBlob(cmd, "@fcg", blobs, "FCG");
        AddBlob(cmd, "@dis", blobs, "DIS");
        AddBlob(cmd, "@cms", blobs, "CMS");
        cmd.ExecuteNonQuery();
        tx.Commit();
    }

    private static void AddBlob(SqliteCommand cmd, String name, Dictionary<String, Byte[]> blobs, String tag)
    {
        if (blobs.TryGetValue(tag, out Byte[]? data) && data.Length > 0)
            cmd.Parameters.AddWithValue(name, data);
        else
            cmd.Parameters.AddWithValue(name, DBNull.Value);
    }
}
