using Microsoft.Data.Sqlite;
using VoteCounter.Models;

namespace VoteCounter.Services;

public sealed class LocalStore
{
    public string RootFolder => LocalDatabase.DatabaseFolder;
    public string DatabaseFile => LocalDatabase.DatabasePath;

    // Оставлены для совместимости со старым кодом/логами: теперь это уже не JSON,
    // а путь к реальной SQLite-базе проекта.
    public string ContestFile => DatabaseFile;
    public string SettingsFile => DatabaseFile;

    public LocalStore()
    {
        LocalDatabase.Initialize();
    }

    public List<Contest> LoadContests()
    {
        using var connection = LocalDatabase.OpenConnection();
        var contests = new List<Contest>();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT Id, Number, Name, Stage, StageUpdatedAt, HostName, NextHostName, StartedAt, ClosedAt, IsActive, VoteLimit, BaseVote, MaxVote, LimitMaxVote,
                       LimitMaxVoteByTopic, OneMaxVotePerTopic, DowngradeExtraMaxVoteToBase,
                       AllowZeroVotes, TreatSelfVoteAsZero, HostKnowsAuthors, CreatedAt, UpdatedAt
                FROM Contests
                ORDER BY IsActive DESC, CAST(Number AS INTEGER), Number, Name;
                """;

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                contests.Add(new Contest
                {
                    Id = GetString(reader, 0),
                    Number = GetString(reader, 1),
                    Name = GetString(reader, 2),
                    Stage = reader.GetInt32(3),
                    StageUpdatedAt = GetDateTime(reader, 4),
                    HostName = GetString(reader, 5),
                    NextHostName = GetString(reader, 6),
                    StartedAt = GetDateTime(reader, 7),
                    ClosedAt = GetNullableDateTime(reader, 8),
                    IsActive = GetBool(reader, 9),
                    VoteLimit = reader.GetInt32(10),
                    BaseVote = reader.GetInt32(11),
                    MaxVote = reader.GetInt32(12),
                    LimitMaxVote = reader.GetInt32(13),
                    LimitMaxVoteByTopic = GetBool(reader, 14),
                    OneMaxVotePerTopic = GetBool(reader, 15),
                    DowngradeExtraMaxVoteToBase = GetBool(reader, 16),
                    AllowZeroVotes = GetBool(reader, 17),
                    TreatSelfVoteAsZero = GetBool(reader, 18),
                    HostKnowsAuthors = GetBool(reader, 19),
                    CreatedAt = GetDateTime(reader, 20),
                    UpdatedAt = GetDateTime(reader, 21)
                });
            }
        }

        foreach (Contest contest in contests)
        {
            contest.Topics = LoadTopics(connection, contest.Id);
            contest.Works = LoadWorks(connection, contest.Id);
            contest.Voters = LoadVoters(connection, contest.Id);
        }

        // ВАЖНО: если база пустая, не создаём демонстрационный конкурс автоматически.
        // Иначе при первом запуске новой чистой базы кажется, что база "пересоздалась"
        // и в ней снова появился служебный конкурс. Пользователь сам создаёт или импортирует конкурс.
        return contests;
    }

    public void SaveContests(IEnumerable<Contest> contests)
    {
        using var connection = LocalDatabase.OpenConnection();
        LocalDatabase.SaveContests(connection, contests);
    }

    public List<VoteEntry> LoadVotes(string contestId)
    {
        using var connection = LocalDatabase.OpenConnection();
        var votes = new List<VoteEntry>();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ContestId, VoterName, VoterKey, WorkNo, ScoreText, Score,
                   OriginalScore, OriginalScoreText, VotedScore, VotedScoreText, AcceptedScore, AcceptedScoreText,
                   WasChangedByRules, RuleNote, Comment, SourceLine, UpdatedAt
            FROM Votes
            WHERE ContestId = $contestId
            ORDER BY VoterName, WorkNo;
            """;
        command.Parameters.AddWithValue("$contestId", contestId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            votes.Add(new VoteEntry
            {
                ContestId = GetString(reader, 0),
                VoterName = GetString(reader, 1),
                VoterKey = GetString(reader, 2),
                WorkNo = reader.GetInt32(3),
                ScoreText = GetString(reader, 4),
                Score = GetDecimal(reader, 5),
                OriginalScore = GetDecimal(reader, 6),
                OriginalScoreText = GetString(reader, 7),
                VotedScore = GetDecimal(reader, 8),
                VotedScoreText = GetString(reader, 9),
                AcceptedScore = GetDecimal(reader, 10),
                AcceptedScoreText = GetString(reader, 11),
                WasChangedByRules = GetBool(reader, 12),
                RuleNote = GetString(reader, 13),
                Comment = GetString(reader, 14),
                SourceLine = GetString(reader, 15),
                UpdatedAt = GetDateTime(reader, 16)
            });
        }

        return votes;
    }

    public void SaveVotes(string contestId, IEnumerable<VoteEntry> votes)
    {
        using var connection = LocalDatabase.OpenConnection();
        LocalDatabase.SaveVotes(connection, contestId, votes);
    }

    public AppSettings LoadSettings()
    {
        using var connection = LocalDatabase.OpenConnection();
        string templatePath = LoadSetting(connection, nameof(AppSettings.TemplatePath));
        string outputFolder = LoadSetting(connection, nameof(AppSettings.OutputFolder));
        string voteDraftContestId = LoadSetting(connection, nameof(AppSettings.VoteDraftContestId));
        string voteDraftText = LoadSetting(connection, nameof(AppSettings.VoteDraftText));
        string voteDraftUpdatedAtText = LoadSetting(connection, nameof(AppSettings.VoteDraftUpdatedAt));
        return new AppSettings
        {
            TemplatePath = templatePath,
            OutputFolder = outputFolder,
            VoteDraftContestId = voteDraftContestId,
            VoteDraftText = voteDraftText,
            VoteDraftUpdatedAt = DateTime.TryParse(voteDraftUpdatedAtText, out DateTime voteDraftUpdatedAt) ? voteDraftUpdatedAt : null
        };
    }

    public void SaveSettings(AppSettings settings)
    {
        using var connection = LocalDatabase.OpenConnection();
        LocalDatabase.SaveSettings(connection, settings);
    }

    public void ClearVotes(string contestId)
    {
        using var connection = LocalDatabase.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Votes WHERE ContestId = $contestId;";
        command.Parameters.AddWithValue("$contestId", contestId);
        command.ExecuteNonQuery();
    }

    public string GetVotesFile(string contestId) => DatabaseFile;

    private static List<ContestTopic> LoadTopics(SqliteConnection connection, string contestId)
    {
        var topics = new List<ContestTopic>();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Number, Title
            FROM ContestTopics
            WHERE ContestId = $contestId
            ORDER BY SortNo, Number;
            """;
        command.Parameters.AddWithValue("$contestId", contestId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            topics.Add(new ContestTopic
            {
                Number = reader.GetInt32(0),
                Title = GetString(reader, 1)
            });
        }

        return topics;
    }

    private static List<ContestWork> LoadWorks(SqliteConnection connection, string contestId)
    {
        var works = new List<ContestWork>();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Number, Title, Author, Topic, Content, HasVotes
            FROM ContestWorks
            WHERE ContestId = $contestId
            ORDER BY SortNo, Number;
            """;
        command.Parameters.AddWithValue("$contestId", contestId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            works.Add(new ContestWork
            {
                Number = reader.GetInt32(0),
                Title = GetString(reader, 1),
                Author = GetString(reader, 2),
                Topic = GetString(reader, 3),
                Content = GetString(reader, 4),
                HasVotes = GetBool(reader, 5)
            });
        }

        return works;
    }

    private static List<VoterSetting> LoadVoters(SqliteConnection connection, string contestId)
    {
        var voters = new List<VoterSetting>();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Name, MustVote, HasVoted
            FROM VoterSettings
            WHERE ContestId = $contestId
            ORDER BY SortNo, Name;
            """;
        command.Parameters.AddWithValue("$contestId", contestId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            voters.Add(new VoterSetting
            {
                Name = GetString(reader, 0),
                MustVote = GetBool(reader, 1),
                HasVoted = GetBool(reader, 2)
            });
        }

        return voters;
    }

    private static string LoadSetting(SqliteConnection connection, string key)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM AppSettings WHERE Key = $key;";
        command.Parameters.AddWithValue("$key", key);
        object? value = command.ExecuteScalar();
        return value as string ?? string.Empty;
    }

    private static string GetString(SqliteDataReader reader, int index)
    {
        return reader.IsDBNull(index) ? string.Empty : reader.GetString(index);
    }

    private static decimal GetDecimal(SqliteDataReader reader, int index)
    {
        if (reader.IsDBNull(index))
            return 0m;

        object value = reader.GetValue(index);
        return value switch
        {
            decimal d => d,
            double d => Convert.ToDecimal(d),
            float f => Convert.ToDecimal(f),
            long l => l,
            int i => i,
            string text when decimal.TryParse(text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal parsed) => parsed,
            _ => Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    private static bool GetBool(SqliteDataReader reader, int index)
    {
        return !reader.IsDBNull(index) && reader.GetInt32(index) != 0;
    }

    private static DateTime GetDateTime(SqliteDataReader reader, int index)
    {
        if (reader.IsDBNull(index))
            return DateTime.Now;

        string value = reader.GetString(index);
        return DateTime.TryParse(value, out DateTime date) ? date : DateTime.Now;
    }

    private static DateTime? GetNullableDateTime(SqliteDataReader reader, int index)
    {
        if (reader.IsDBNull(index))
            return null;

        string value = reader.GetString(index);
        return DateTime.TryParse(value, out DateTime date) ? date : null;
    }
}

public sealed class AppSettings
{
    public string TemplatePath { get; set; } = string.Empty;
    public string OutputFolder { get; set; } = string.Empty;
    public string VoteDraftContestId { get; set; } = string.Empty;
    public string VoteDraftText { get; set; } = string.Empty;
    public DateTime? VoteDraftUpdatedAt { get; set; }
}
