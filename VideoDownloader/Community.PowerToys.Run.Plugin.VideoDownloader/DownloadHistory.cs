using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace Community.PowerToys.Run.Plugin.VideoDownloader
{
    internal class DownloadHistory
    {
        private readonly string _dbPath;

        public DownloadHistory(string dbPath)
        {
            _dbPath = dbPath;
            EnsureDatabase();
        }

        private void EnsureDatabase()
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"CREATE TABLE IF NOT EXISTS History(
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Url TEXT,
                FilePath TEXT,
                Success INTEGER,
                Timestamp TEXT,
                Format TEXT
            );";
            command.ExecuteNonQuery();
        }

        public void Log(string url, string filePath, bool success, string format)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO History(Url, FilePath, Success, Timestamp, Format)
                                    VALUES($u,$f,$s,$t,$fmt);";
            command.Parameters.AddWithValue("$u", url);
            command.Parameters.AddWithValue("$f", filePath);
            command.Parameters.AddWithValue("$s", success ? 1 : 0);
            command.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("o"));
            command.Parameters.AddWithValue("$fmt", format);
            command.ExecuteNonQuery();
        }

        public IEnumerable<(string Url, string FilePath, bool Success, string Timestamp, string Format)> GetLast(int count)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"SELECT Url, FilePath, Success, Timestamp, Format FROM History ORDER BY Id DESC LIMIT $c;";
            command.Parameters.AddWithValue("$c", count);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                yield return (
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetInt32(2) == 1,
                    reader.GetString(3),
                    reader.GetString(4)
                );
            }
        }
    }
}
