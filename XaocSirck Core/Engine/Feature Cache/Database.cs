using System.Data;
using Microsoft.Data.Sqlite;
using XaocSirck_Core.Interface.Feature;

namespace XaocSirck_Core.Engine.Feature_Cache;

public class DatabaseManagement : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly String _connectionString;
    private Boolean _disposed;

    private const String TableName = "FeatureCache";

    public const Int32 RbLength = 131072;
    public const Int32 EmLength = 64;
    public const Int32 ItLength = 417;
    public const Int32 AlLength = 98304;
    public const Int32 ZfLength = 256;

    public DatabaseManagement(String dbPath)
    {
        String? dir = Path.GetDirectoryName(dbPath);
        if (!String.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _connectionString = $"Data Source={dbPath};";
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();
        InitializeTable();
    }

    private void InitializeTable()
    {
        String createTableSql = $@"
            CREATE TABLE IF NOT EXISTS {TableName} (
                Hash TEXT PRIMARY KEY NOT NULL,
                RB BLOB NOT NULL,
                EM BLOB NOT NULL,
                IT BLOB NOT NULL,
                AL BLOB NOT NULL,
                ZF BLOB NOT NULL,
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
            );";

        using SqliteCommand command = new(createTableSql, _connection);
        command.ExecuteNonQuery();
    }

    public void InsertOrUpdate(
        String sha256Hash,
        Single[] rbData,
        Single[] emData,
        Single[] itData,
        Single[] alData,
        Single[] zfData)
    {
        ValidateInputs(sha256Hash, rbData, emData, itData, alData, zfData);

        String upsertSql = $@"
            INSERT INTO {TableName} (Hash, RB, EM, IT, AL, ZF)
            VALUES (@hash, @rb, @em, @it, @al, @zf)
            ON CONFLICT(Hash) DO UPDATE SET
                RB = excluded.RB,
                EM = excluded.EM,
                IT = excluded.IT,
                AL = excluded.AL,
                ZF = excluded.ZF,
                CreatedAt = CURRENT_TIMESTAMP;";

        using SqliteCommand command = new(upsertSql, _connection);
        command.Parameters.AddWithValue("@hash", sha256Hash);
        command.Parameters.AddWithValue("@rb", FloatArrayToByteArray(rbData));
        command.Parameters.AddWithValue("@em", FloatArrayToByteArray(emData));
        command.Parameters.AddWithValue("@it", FloatArrayToByteArray(itData));
        command.Parameters.AddWithValue("@al", FloatArrayToByteArray(alData));
        command.Parameters.AddWithValue("@zf", FloatArrayToByteArray(zfData));

        command.ExecuteNonQuery();
    }

    public void Insert(
        String sha256Hash,
        Single[] rbData,
        Single[] emData,
        Single[] itData,
        Single[] alData,
        Single[] zfData)
    {
        ValidateInputs(sha256Hash, rbData, emData, itData, alData, zfData);

        String insertSql = $@"
            INSERT INTO {TableName} (Hash, RB, EM, IT, AL, ZF)
            VALUES (@hash, @rb, @em, @it, @al, @zf);";

        using SqliteCommand command = new(insertSql, _connection);
        command.Parameters.AddWithValue("@hash", sha256Hash);
        command.Parameters.AddWithValue("@rb", FloatArrayToByteArray(rbData));
        command.Parameters.AddWithValue("@em", FloatArrayToByteArray(emData));
        command.Parameters.AddWithValue("@it", FloatArrayToByteArray(itData));
        command.Parameters.AddWithValue("@al", FloatArrayToByteArray(alData));
        command.Parameters.AddWithValue("@zf", FloatArrayToByteArray(zfData));

        command.ExecuteNonQuery();
    }

    public void Update(
        String sha256Hash,
        Single[] rbData,
        Single[] emData,
        Single[] itData,
        Single[] alData,
        Single[] zfData)
    {
        ValidateInputs(sha256Hash, rbData, emData, itData, alData, zfData);

        String updateSql = $@"
            UPDATE {TableName} 
            SET RB = @rb, EM = @em, IT = @it, AL = @al, ZF = @zf
            WHERE Hash = @hash;";

        using SqliteCommand command = new(updateSql, _connection);
        command.Parameters.AddWithValue("@hash", sha256Hash);
        command.Parameters.AddWithValue("@rb", FloatArrayToByteArray(rbData));
        command.Parameters.AddWithValue("@em", FloatArrayToByteArray(emData));
        command.Parameters.AddWithValue("@it", FloatArrayToByteArray(itData));
        command.Parameters.AddWithValue("@al", FloatArrayToByteArray(alData));
        command.Parameters.AddWithValue("@zf", FloatArrayToByteArray(zfData));

        Int32 affected = command.ExecuteNonQuery();
        if (affected == 0)
            throw new KeyNotFoundException($"Hash not found: {sha256Hash}");
    }

    public void BatchInsert(List<(String hash, Single[] rb, Single[] em, Single[] it, Single[] al, Single[] zf)> records)
    {
        using SqliteTransaction transaction = _connection.BeginTransaction();
        try
        {
            String insertSql = $@"
                    INSERT INTO {TableName} (Hash, RB, EM, IT, AL, ZF)
                    VALUES (@hash, @rb, @em, @it, @al, @zf);";

            using (SqliteCommand command = new(insertSql, _connection, transaction))
            {
                SqliteParameter hashParam = command.Parameters.Add("@hash", SqliteType.Text);
                SqliteParameter rbParam = command.Parameters.Add("@rb", SqliteType.Blob);
                SqliteParameter emParam = command.Parameters.Add("@em", SqliteType.Blob);
                SqliteParameter itParam = command.Parameters.Add("@it", SqliteType.Blob);
                SqliteParameter alParam = command.Parameters.Add("@al", SqliteType.Blob);
                SqliteParameter zfParam = command.Parameters.Add("@zf", SqliteType.Blob);

                foreach ((String hash, Single[] rb, Single[] em, Single[] it, Single[] al, Single[] zf) in records)
                {
                    ValidateInputs(hash, rb, em, it, al, zf);

                    hashParam.Value = hash;
                    rbParam.Value = FloatArrayToByteArray(rb);
                    emParam.Value = FloatArrayToByteArray(em);
                    itParam.Value = FloatArrayToByteArray(it);
                    alParam.Value = FloatArrayToByteArray(al);
                    zfParam.Value = FloatArrayToByteArray(zf);

                    command.ExecuteNonQuery();
                }
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public FeatureRecord? Read(String sha256Hash)
    {
        String selectSql = $@"
            SELECT Hash, RB, EM, IT, AL, ZF, CreatedAt 
            FROM {TableName} 
            WHERE Hash = @hash;";

        using SqliteCommand command = new(selectSql, _connection);
        command.Parameters.AddWithValue("@hash", sha256Hash);

        using SqliteDataReader reader = command.ExecuteReader();
        if (reader.Read())
        {
            return ReadRecordFromReader(reader);
        }
        return null;
    }

    public List<FeatureRecord> ReadAll()
    {
        List<FeatureRecord> results = [];
        String selectSql = $@"
            SELECT Hash, RB, EM, IT, AL, ZF, CreatedAt 
            FROM {TableName};";

        using (SqliteCommand command = new(selectSql, _connection))
        using (SqliteDataReader reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                results.Add(ReadRecordFromReader(reader));
            }
        }
        return results;
    }

    public List<FeatureRecord> ReadPaged(Int32 pageIndex, Int32 pageSize)
    {
        List<FeatureRecord> results = [];
        String selectSql = $@"
            SELECT Hash, RB, EM, IT, AL, ZF, CreatedAt 
            FROM {TableName}
            ORDER BY CreatedAt DESC
            LIMIT @limit OFFSET @offset;";

        using (SqliteCommand command = new(selectSql, _connection))
        {
            command.Parameters.AddWithValue("@limit", pageSize);
            command.Parameters.AddWithValue("@offset", pageIndex * pageSize);

            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add(ReadRecordFromReader(reader));
            }
        }
        return results;
    }

    public Boolean Exists(String sha256Hash)
    {
        String sql = $@"
            SELECT 1 FROM {TableName} 
            WHERE Hash = @hash LIMIT 1;";

        using SqliteCommand command = new(sql, _connection);
        command.Parameters.AddWithValue("@hash", sha256Hash);
        return command.ExecuteScalar() != null;
    }

    public Int32 Count()
    {
        String sql = $@"
            SELECT COUNT(*) FROM {TableName};";

        using SqliteCommand command = new(sql, _connection);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public List<String> ReadAllHashes()
    {
        List<String> hashes = [];
        String sql = $@"
            SELECT Hash FROM {TableName} 
            ORDER BY CreatedAt DESC;";

        using (SqliteCommand command = new(sql, _connection))
        using (SqliteDataReader reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                hashes.Add(reader.GetString(0));
            }
        }
        return hashes;
    }

    public void Delete(String sha256Hash)
    {
        String deleteSql = $@"
            DELETE FROM {TableName} 
            WHERE Hash = @hash;";

        using SqliteCommand command = new(deleteSql, _connection);
        command.Parameters.AddWithValue("@hash", sha256Hash);
        command.ExecuteNonQuery();
    }

    public void ClearAll()
    {
        String sql = $@"
            DELETE FROM {TableName};";

        using SqliteCommand command = new(sql, _connection);
        command.ExecuteNonQuery();
    }

    private void ValidateInputs(String sha256Hash, Single[] rbData, Single[] emData, Single[] itData, Single[] alData, Single[] zfData)
    {
        if (String.IsNullOrWhiteSpace(sha256Hash))
            throw new ArgumentException("SHA256 hash cannot be null or empty", nameof(sha256Hash));

        if (rbData == null || rbData.Length != RbLength)
            throw new ArgumentException($"RB must be exactly {RbLength} floats", nameof(rbData));

        if (emData == null || emData.Length != EmLength)
            throw new ArgumentException($"EM must be exactly {EmLength} floats", nameof(emData));

        if (itData == null || itData.Length != ItLength)
            throw new ArgumentException($"IT must be exactly {ItLength} floats", nameof(itData));

        if (alData == null || alData.Length != AlLength)
            throw new ArgumentException($"AL must be exactly {AlLength} floats", nameof(alData));

        if (zfData == null || zfData.Length != ZfLength)
            throw new ArgumentException($"ZF must be exactly {ZfLength} floats", nameof(zfData));
    }

    private Byte[] FloatArrayToByteArray(Single[] floatArray)
    {
        Byte[] byteArray = new Byte[floatArray.Length * sizeof(Single)];
        Buffer.BlockCopy(floatArray, 0, byteArray, 0, byteArray.Length);
        return byteArray;
    }

    private Single[] ByteArrayToFloatArray(Byte[] byteArray, Int32 expectedLength)
    {
        Single[] floatArray = new Single[expectedLength];
        Buffer.BlockCopy(byteArray, 0, floatArray, 0, byteArray.Length);
        return floatArray;
    }

    private FeatureRecord ReadRecordFromReader(SqliteDataReader reader)
    {
        return new FeatureRecord
        {
            Hash = reader.GetString(0),
            RB = ByteArrayToFloatArray((Byte[])reader["RB"], RbLength),
            EM = ByteArrayToFloatArray((Byte[])reader["EM"], EmLength),
            IT = ByteArrayToFloatArray((Byte[])reader["IT"], ItLength),
            AL = ByteArrayToFloatArray((Byte[])reader["AL"], AlLength),
            ZF = ByteArrayToFloatArray((Byte[])reader["ZF"], ZfLength),
            CreatedAt = reader.GetDateTime(6)
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _connection?.Close();
            _connection?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
