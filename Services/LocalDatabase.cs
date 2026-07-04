using System.Text;
using Microsoft.Data.Sqlite;
using VoteCounter.Models;

namespace VoteCounter.Services;

internal static class LocalDatabase
{
    private const int CurrentSchemaVersion = 21;
    private static readonly object SyncRoot = new();
    private static bool _initialized;

    public static string ProjectFolder { get; } = ResolveProjectFolder();
    public static string DatabaseFolder { get; } = Path.Combine(ProjectFolder, "Database");
    public static string DatabasePath { get; } = Path.Combine(DatabaseFolder, "VoteCounter.db");
    public static SqliteConnection OpenConnection()
    {
        Initialize();
        var connection = new SqliteConnection($"Data Source={DatabasePath};Cache=Shared");
        connection.Open();

        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    public static void Initialize()
    {
        if (_initialized)
            return;

        lock (SyncRoot)
        {
            if (_initialized)
                return;

            Directory.CreateDirectory(DatabaseFolder);
            using var connection = new SqliteConnection($"Data Source={DatabasePath};Cache=Shared");
            connection.Open();
            ExecuteNonQuery(connection, "PRAGMA foreign_keys = ON;");
            CreateSchema(connection);
            _initialized = true;
        }
    }

    private static void CreateSchema(SqliteConnection connection)
    {
        ExecuteNonQuery(connection, """
            CREATE TABLE IF NOT EXISTS SchemaInfo(
                Key TEXT NOT NULL PRIMARY KEY,
                Value TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS AppSettings(
                Key TEXT NOT NULL PRIMARY KEY,
                Value TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Setting(
                Id INTEGER NOT NULL PRIMARY KEY CHECK(Id = 1),
                Ini BLOB NULL,
                UpdatedAt TEXT NOT NULL
            );

            INSERT OR IGNORE INTO Setting(Id, Ini, UpdatedAt)
            VALUES(1, NULL, strftime('%Y-%m-%dT%H:%M:%fZ','now'));

            CREATE TABLE IF NOT EXISTS Contests(
                Id TEXT NOT NULL PRIMARY KEY,
                Number TEXT NOT NULL,
                Name TEXT NOT NULL,
                Stage INTEGER NOT NULL DEFAULT 1,
                StageUpdatedAt TEXT NOT NULL DEFAULT '',
                HostName TEXT NOT NULL DEFAULT '',
                NextHostName TEXT NOT NULL DEFAULT '',
                StartedAt TEXT NOT NULL DEFAULT '',
                ClosedAt TEXT NOT NULL DEFAULT '',
                IsActive INTEGER NOT NULL DEFAULT 1,
                VoteLimit INTEGER NOT NULL,
                BaseVote INTEGER NOT NULL,
                MaxVote INTEGER NOT NULL,
                LimitMaxVote INTEGER NOT NULL,
                LimitMaxVoteByTopic INTEGER NOT NULL,
                OneMaxVotePerTopic INTEGER NOT NULL,
                DowngradeExtraMaxVoteToBase INTEGER NOT NULL,
                AllowZeroVotes INTEGER NOT NULL,
                TreatSelfVoteAsZero INTEGER NOT NULL,
                HostKnowsAuthors INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ContestTopics(
                ContestId TEXT NOT NULL,
                Number INTEGER NOT NULL,
                Title TEXT NOT NULL,
                SortNo INTEGER NOT NULL,
                PRIMARY KEY(ContestId, Number),
                FOREIGN KEY(ContestId) REFERENCES Contests(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS ContestWorks(
                ContestId TEXT NOT NULL,
                Number INTEGER NOT NULL,
                Title TEXT NOT NULL,
                Author TEXT NOT NULL,
                Topic TEXT NOT NULL,
                Content TEXT NOT NULL,
                SortNo INTEGER NOT NULL,
                PRIMARY KEY(ContestId, Number),
                FOREIGN KEY(ContestId) REFERENCES Contests(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS VoterSettings(
                ContestId TEXT NOT NULL,
                Name TEXT NOT NULL,
                MustVote INTEGER NOT NULL,
                SortNo INTEGER NOT NULL,
                PRIMARY KEY(ContestId, Name),
                FOREIGN KEY(ContestId) REFERENCES Contests(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Votes(
                ContestId TEXT NOT NULL,
                VoterKey TEXT NOT NULL,
                VoterName TEXT NOT NULL,
                WorkNo INTEGER NOT NULL,
                ScoreText TEXT NOT NULL,
                Score REAL NOT NULL,
                OriginalScore REAL NOT NULL,
                OriginalScoreText TEXT NOT NULL,
                VotedScore REAL NOT NULL DEFAULT 0,
                VotedScoreText TEXT NOT NULL DEFAULT '',
                AcceptedScore REAL NOT NULL DEFAULT 0,
                AcceptedScoreText TEXT NOT NULL DEFAULT '',
                WasChangedByRules INTEGER NOT NULL,
                RuleNote TEXT NOT NULL,
                Comment TEXT NOT NULL,
                SourceLine TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                PRIMARY KEY(ContestId, VoterKey, WorkNo),
                FOREIGN KEY(ContestId) REFERENCES Contests(Id) ON DELETE CASCADE
            );

            INSERT INTO SchemaInfo(Key, Value)
            VALUES('SchemaVersion', '21')
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
            """);

        EnsureColumn(connection, "Contests", "Stage", "INTEGER NOT NULL DEFAULT 1");
        EnsureColumn(connection, "Contests", "StageUpdatedAt", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "Contests", "HostName", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "Contests", "NextHostName", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "Contests", "StartedAt", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "Contests", "ClosedAt", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "Contests", "IsActive", "INTEGER NOT NULL DEFAULT 1");
        EnsureColumn(connection, "Votes", "VotedScore", "REAL NOT NULL DEFAULT 0");
        EnsureColumn(connection, "Votes", "VotedScoreText", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "Votes", "AcceptedScore", "REAL NOT NULL DEFAULT 0");
        EnsureColumn(connection, "Votes", "AcceptedScoreText", "TEXT NOT NULL DEFAULT ''");
    }

    private static void EnsureColumn(SqliteConnection connection, string tableName, string columnName, string definition)
    {
        using (var check = connection.CreateCommand())
        {
            check.CommandText = $"PRAGMA table_info({tableName});";
            using var reader = check.ExecuteReader();
            while (reader.Read())
            {
                string existing = reader.GetString(1);
                if (string.Equals(existing, columnName, StringComparison.OrdinalIgnoreCase))
                    return;
            }
        }

        ExecuteNonQuery(connection, $"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition};");
    }

    private static List<Contest> NormalizeActiveContest(IEnumerable<Contest> contests)
    {
        var contestList = contests.ToList();
        if (contestList.Count == 0)
            return contestList;

        foreach (Contest contest in contestList)
        {
            if (contest.CreatedAt == default)
                contest.CreatedAt = DateTime.Now;
            if (contest.StartedAt == default)
                contest.StartedAt = contest.CreatedAt;
            if (contest.StageUpdatedAt == default)
                contest.StageUpdatedAt = contest.StartedAt;

            if (contest.Stage == (int)ContestStage.Finished)
            {
                contest.IsActive = false;
                contest.ClosedAt ??= contest.StageUpdatedAt == default ? DateTime.Now : contest.StageUpdatedAt;
            }
        }

        Contest? active = contestList
            .Where(x => x.IsActive && x.Stage != (int)ContestStage.Finished)
            .OrderByDescending(x => x.StageUpdatedAt)
            .ThenByDescending(x => x.UpdatedAt)
            .FirstOrDefault();

        active ??= contestList
            .Where(x => x.Stage != (int)ContestStage.Finished)
            .OrderByDescending(x => x.StageUpdatedAt)
            .ThenByDescending(x => x.UpdatedAt)
            .FirstOrDefault();

        foreach (Contest contest in contestList)
            contest.IsActive = active is not null && string.Equals(contest.Id, active.Id, StringComparison.OrdinalIgnoreCase);

        return contestList;
    }

    internal static void SaveContests(SqliteConnection connection, IEnumerable<Contest> contests)
    {
        var contestList = NormalizeActiveContest(contests)
            .OrderBy(x => x.Number)
            .ThenBy(x => x.Name)
            .ToList();

        using var transaction = connection.BeginTransaction();

        var incomingIds = contestList.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingIds = new List<string>();
        using (var readIds = connection.CreateCommand())
        {
            readIds.Transaction = transaction;
            readIds.CommandText = "SELECT Id FROM Contests;";
            using var reader = readIds.ExecuteReader();
            while (reader.Read())
                existingIds.Add(reader.GetString(0));
        }

        foreach (string existingId in existingIds.Where(x => !incomingIds.Contains(x)))
        {
            using var delete = connection.CreateCommand();
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM Contests WHERE Id = $id;";
            delete.Parameters.AddWithValue("$id", existingId);
            delete.ExecuteNonQuery();
        }

        foreach (Contest contest in contestList)
        {
            contest.UpdatedAt = contest.UpdatedAt == default ? DateTime.Now : contest.UpdatedAt;
            contest.CreatedAt = contest.CreatedAt == default ? DateTime.Now : contest.CreatedAt;

            using (var upsert = connection.CreateCommand())
            {
                upsert.Transaction = transaction;
                upsert.CommandText = """
                    INSERT INTO Contests(
                        Id, Number, Name, Stage, StageUpdatedAt, HostName, NextHostName, StartedAt, ClosedAt, IsActive, VoteLimit, BaseVote, MaxVote, LimitMaxVote,
                        LimitMaxVoteByTopic, OneMaxVotePerTopic, DowngradeExtraMaxVoteToBase,
                        AllowZeroVotes, TreatSelfVoteAsZero, HostKnowsAuthors, CreatedAt, UpdatedAt)
                    VALUES(
                        $id, $number, $name, $stage, $stageUpdatedAt, $hostName, $nextHostName, $startedAt, $closedAt, $isActive, $voteLimit, $baseVote, $maxVote, $limitMaxVote,
                        $limitMaxVoteByTopic, $oneMaxVotePerTopic, $downgradeExtraMaxVoteToBase,
                        $allowZeroVotes, $treatSelfVoteAsZero, $hostKnowsAuthors, $createdAt, $updatedAt)
                    ON CONFLICT(Id) DO UPDATE SET
                        Number = excluded.Number,
                        Name = excluded.Name,
                        Stage = excluded.Stage,
                        StageUpdatedAt = excluded.StageUpdatedAt,
                        HostName = excluded.HostName,
                        NextHostName = excluded.NextHostName,
                        StartedAt = excluded.StartedAt,
                        ClosedAt = excluded.ClosedAt,
                        IsActive = excluded.IsActive,
                        VoteLimit = excluded.VoteLimit,
                        BaseVote = excluded.BaseVote,
                        MaxVote = excluded.MaxVote,
                        LimitMaxVote = excluded.LimitMaxVote,
                        LimitMaxVoteByTopic = excluded.LimitMaxVoteByTopic,
                        OneMaxVotePerTopic = excluded.OneMaxVotePerTopic,
                        DowngradeExtraMaxVoteToBase = excluded.DowngradeExtraMaxVoteToBase,
                        AllowZeroVotes = excluded.AllowZeroVotes,
                        TreatSelfVoteAsZero = excluded.TreatSelfVoteAsZero,
                        HostKnowsAuthors = excluded.HostKnowsAuthors,
                        CreatedAt = excluded.CreatedAt,
                        UpdatedAt = excluded.UpdatedAt;
                    """;
                upsert.Parameters.AddWithValue("$id", contest.Id);
                upsert.Parameters.AddWithValue("$number", contest.Number ?? string.Empty);
                upsert.Parameters.AddWithValue("$name", contest.Name ?? string.Empty);
                upsert.Parameters.AddWithValue("$stage", contest.Stage <= 0 ? (int)ContestStage.TopicReception : contest.Stage);
                upsert.Parameters.AddWithValue("$stageUpdatedAt", (contest.StageUpdatedAt == default ? DateTime.Now : contest.StageUpdatedAt).ToString("O"));
                upsert.Parameters.AddWithValue("$hostName", contest.HostName ?? string.Empty);
                upsert.Parameters.AddWithValue("$nextHostName", contest.NextHostName ?? string.Empty);
                upsert.Parameters.AddWithValue("$startedAt", (contest.StartedAt == default ? contest.CreatedAt : contest.StartedAt).ToString("O"));
                upsert.Parameters.AddWithValue("$closedAt", contest.ClosedAt?.ToString("O") ?? string.Empty);
                upsert.Parameters.AddWithValue("$isActive", ToDbBool(contest.IsActive));
                upsert.Parameters.AddWithValue("$voteLimit", contest.VoteLimit);
                upsert.Parameters.AddWithValue("$baseVote", contest.BaseVote);
                upsert.Parameters.AddWithValue("$maxVote", contest.MaxVote);
                upsert.Parameters.AddWithValue("$limitMaxVote", contest.LimitMaxVote);
                upsert.Parameters.AddWithValue("$limitMaxVoteByTopic", ToDbBool(contest.LimitMaxVoteByTopic));
                upsert.Parameters.AddWithValue("$oneMaxVotePerTopic", ToDbBool(contest.OneMaxVotePerTopic));
                upsert.Parameters.AddWithValue("$downgradeExtraMaxVoteToBase", ToDbBool(contest.DowngradeExtraMaxVoteToBase));
                upsert.Parameters.AddWithValue("$allowZeroVotes", ToDbBool(contest.AllowZeroVotes));
                upsert.Parameters.AddWithValue("$treatSelfVoteAsZero", ToDbBool(contest.TreatSelfVoteAsZero));
                upsert.Parameters.AddWithValue("$hostKnowsAuthors", ToDbBool(contest.HostKnowsAuthors));
                upsert.Parameters.AddWithValue("$createdAt", contest.CreatedAt.ToString("O"));
                upsert.Parameters.AddWithValue("$updatedAt", contest.UpdatedAt.ToString("O"));
                upsert.ExecuteNonQuery();
            }

            using (var deleteTopics = connection.CreateCommand())
            {
                deleteTopics.Transaction = transaction;
                deleteTopics.CommandText = "DELETE FROM ContestTopics WHERE ContestId = $contestId;";
                deleteTopics.Parameters.AddWithValue("$contestId", contest.Id);
                deleteTopics.ExecuteNonQuery();
            }

            int topicSortNo = 0;
            foreach (ContestTopic topic in contest.Topics.OrderBy(x => x.Number))
            {
                using var insertTopic = connection.CreateCommand();
                insertTopic.Transaction = transaction;
                insertTopic.CommandText = """
                    INSERT OR REPLACE INTO ContestTopics(ContestId, Number, Title, SortNo)
                    VALUES($contestId, $number, $title, $sortNo);
                    """;
                insertTopic.Parameters.AddWithValue("$contestId", contest.Id);
                insertTopic.Parameters.AddWithValue("$number", topic.Number);
                insertTopic.Parameters.AddWithValue("$title", topic.Title ?? string.Empty);
                insertTopic.Parameters.AddWithValue("$sortNo", topicSortNo++);
                insertTopic.ExecuteNonQuery();
            }

            using (var deleteWorks = connection.CreateCommand())
            {
                deleteWorks.Transaction = transaction;
                deleteWorks.CommandText = "DELETE FROM ContestWorks WHERE ContestId = $contestId;";
                deleteWorks.Parameters.AddWithValue("$contestId", contest.Id);
                deleteWorks.ExecuteNonQuery();
            }

            int workSortNo = 0;
            foreach (ContestWork work in contest.Works.OrderBy(x => x.Number))
            {
                using var insertWork = connection.CreateCommand();
                insertWork.Transaction = transaction;
                insertWork.CommandText = """
                    INSERT OR REPLACE INTO ContestWorks(ContestId, Number, Title, Author, Topic, Content, SortNo)
                    VALUES($contestId, $number, $title, $author, $topic, $content, $sortNo);
                    """;
                insertWork.Parameters.AddWithValue("$contestId", contest.Id);
                insertWork.Parameters.AddWithValue("$number", work.Number);
                insertWork.Parameters.AddWithValue("$title", work.Title ?? string.Empty);
                insertWork.Parameters.AddWithValue("$author", work.Author ?? string.Empty);
                insertWork.Parameters.AddWithValue("$topic", work.Topic ?? string.Empty);
                insertWork.Parameters.AddWithValue("$content", work.Content ?? string.Empty);
                insertWork.Parameters.AddWithValue("$sortNo", workSortNo++);
                insertWork.ExecuteNonQuery();
            }

            using (var deleteVoters = connection.CreateCommand())
            {
                deleteVoters.Transaction = transaction;
                deleteVoters.CommandText = "DELETE FROM VoterSettings WHERE ContestId = $contestId;";
                deleteVoters.Parameters.AddWithValue("$contestId", contest.Id);
                deleteVoters.ExecuteNonQuery();
            }

            int voterSortNo = 0;
            foreach (VoterSetting voter in contest.Voters.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
            {
                using var insertVoter = connection.CreateCommand();
                insertVoter.Transaction = transaction;
                insertVoter.CommandText = """
                    INSERT OR REPLACE INTO VoterSettings(ContestId, Name, MustVote, SortNo)
                    VALUES($contestId, $name, $mustVote, $sortNo);
                    """;
                insertVoter.Parameters.AddWithValue("$contestId", contest.Id);
                insertVoter.Parameters.AddWithValue("$name", voter.Name.Trim());
                insertVoter.Parameters.AddWithValue("$mustVote", ToDbBool(voter.MustVote));
                insertVoter.Parameters.AddWithValue("$sortNo", voterSortNo++);
                insertVoter.ExecuteNonQuery();
            }
        }

        transaction.Commit();
    }

    internal static void SaveVotes(SqliteConnection connection, string contestId, IEnumerable<VoteEntry> votes)
    {
        using var transaction = connection.BeginTransaction();
        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM Votes WHERE ContestId = $contestId;";
            delete.Parameters.AddWithValue("$contestId", contestId);
            delete.ExecuteNonQuery();
        }

        foreach (VoteEntry vote in votes.OrderBy(x => x.VoterName).ThenBy(x => x.WorkNo))
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT OR REPLACE INTO Votes(
                    ContestId, VoterKey, VoterName, WorkNo, ScoreText, Score,
                    OriginalScore, OriginalScoreText, VotedScore, VotedScoreText, AcceptedScore, AcceptedScoreText, WasChangedByRules, RuleNote,
                    Comment, SourceLine, UpdatedAt)
                VALUES(
                    $contestId, $voterKey, $voterName, $workNo, $scoreText, $score,
                    $originalScore, $originalScoreText, $votedScore, $votedScoreText, $acceptedScore, $acceptedScoreText, $wasChangedByRules, $ruleNote,
                    $comment, $sourceLine, $updatedAt);
                """;
            string voterKey = string.IsNullOrWhiteSpace(vote.VoterKey)
                ? NameNormalizer.Normalize(vote.VoterName)
                : vote.VoterKey;
            insert.Parameters.AddWithValue("$contestId", contestId);
            insert.Parameters.AddWithValue("$voterKey", voterKey);
            insert.Parameters.AddWithValue("$voterName", vote.VoterName ?? string.Empty);
            insert.Parameters.AddWithValue("$workNo", vote.WorkNo);
            insert.Parameters.AddWithValue("$scoreText", vote.ScoreText ?? string.Empty);
            insert.Parameters.AddWithValue("$score", Convert.ToDouble(vote.Score));
            decimal votedScore = vote.VotedScore != 0m || !string.IsNullOrWhiteSpace(vote.VotedScoreText) ? vote.VotedScore : vote.OriginalScore;
            string votedScoreText = !string.IsNullOrWhiteSpace(vote.VotedScoreText) ? vote.VotedScoreText : vote.OriginalScoreText ?? string.Empty;
            decimal acceptedScore = vote.AcceptedScore != 0m || !string.IsNullOrWhiteSpace(vote.AcceptedScoreText) ? vote.AcceptedScore : vote.Score;
            string acceptedScoreText = !string.IsNullOrWhiteSpace(vote.AcceptedScoreText) ? vote.AcceptedScoreText : vote.ScoreText ?? string.Empty;
            insert.Parameters.AddWithValue("$originalScore", Convert.ToDouble(vote.OriginalScore));
            insert.Parameters.AddWithValue("$originalScoreText", vote.OriginalScoreText ?? string.Empty);
            insert.Parameters.AddWithValue("$votedScore", Convert.ToDouble(votedScore));
            insert.Parameters.AddWithValue("$votedScoreText", votedScoreText ?? string.Empty);
            insert.Parameters.AddWithValue("$acceptedScore", Convert.ToDouble(acceptedScore));
            insert.Parameters.AddWithValue("$acceptedScoreText", acceptedScoreText ?? string.Empty);
            insert.Parameters.AddWithValue("$wasChangedByRules", ToDbBool(vote.WasChangedByRules));
            insert.Parameters.AddWithValue("$ruleNote", vote.RuleNote ?? string.Empty);
            insert.Parameters.AddWithValue("$comment", vote.Comment ?? string.Empty);
            insert.Parameters.AddWithValue("$sourceLine", vote.SourceLine ?? string.Empty);
            insert.Parameters.AddWithValue("$updatedAt", (vote.UpdatedAt == default ? DateTime.Now : vote.UpdatedAt).ToString("O"));
            insert.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    internal static void SaveSettings(SqliteConnection connection, AppSettings settings)
    {
        using var transaction = connection.BeginTransaction();
        UpsertSetting(connection, transaction, nameof(AppSettings.TemplatePath), settings.TemplatePath ?? string.Empty);
        UpsertSetting(connection, transaction, nameof(AppSettings.OutputFolder), settings.OutputFolder ?? string.Empty);
        transaction.Commit();
    }

    internal static string LoadLayoutIni()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Ini FROM Setting WHERE Id = 1;";
        object? value = command.ExecuteScalar();

        return value switch
        {
            byte[] bytes when bytes.Length > 0 => Encoding.UTF8.GetString(bytes),
            string text when !string.IsNullOrEmpty(text) => text,
            _ => string.Empty
        };
    }

    internal static void SaveLayoutIni(string iniText)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(iniText ?? string.Empty);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Setting(Id, Ini, UpdatedAt)
            VALUES(1, $ini, $updatedAt)
            ON CONFLICT(Id) DO UPDATE SET
                Ini = excluded.Ini,
                UpdatedAt = excluded.UpdatedAt;
            """;
        command.Parameters.Add("$ini", SqliteType.Blob).Value = bytes;
        command.Parameters.AddWithValue("$updatedAt", DateTime.Now.ToString("O"));
        command.ExecuteNonQuery();
    }

    internal static void DeleteLayoutIni()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Setting(Id, Ini, UpdatedAt)
            VALUES(1, NULL, $updatedAt)
            ON CONFLICT(Id) DO UPDATE SET
                Ini = NULL,
                UpdatedAt = excluded.UpdatedAt;
            """;
        command.Parameters.AddWithValue("$updatedAt", DateTime.Now.ToString("O"));
        command.ExecuteNonQuery();
    }

    private static void UpsertSetting(SqliteConnection connection, SqliteTransaction transaction, string key, string value)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO AppSettings(Key, Value)
            VALUES($key, $value)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    private static string ResolveProjectFolder()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "VoteCounter.csproj")))
                return current.FullName;
            current = current.Parent;
        }

        return AppContext.BaseDirectory;
    }

    private static void ExecuteNonQuery(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static int ExecuteScalarInt(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        object? value = command.ExecuteScalar();
        return value is null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
    }

    private static int ToDbBool(bool value) => value ? 1 : 0;
}
