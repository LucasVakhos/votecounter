using Rhymers.Core.Services;
using System.Security.Cryptography;
using System.Text;
using FirebirdSql.Data.FirebirdClient;
using Rhymers.Core.Models;

namespace Rhymers.Data.Database;

public sealed class FirebirdLegacyImporter
{
    public FirebirdImportReport Import(string databasePath, string user, string password, bool importVotes)
    {
        if (string.IsNullOrWhiteSpace(databasePath) || !File.Exists(databasePath))
            throw new FileNotFoundException("Файл старой Firebird-базы не найден.", databasePath);

        using FbConnection connection = OpenBestConnection(databasePath, user, password, out string mode);
        var report = new FirebirdImportReport { ConnectionMode = mode };
        var schema = ReadSchema(connection);

        string? contestsTable = FindTable(schema, "CONCURSES", "CONTESTS", "CONCURS");
        string? contestNamesTable = FindTable(schema, "CONCURSES_NAMES", "CONCURS_NAMES", "CONTEST_NAMES");
        string? detailsTable = FindTable(schema, "CONCURS_DTL", "CONCURSES_DTL", "CONTEST_DETAILS", "DETAILS");
        string? poetsTable = FindTable(schema, "POETS", "POET", "AUTHORS", "USERS");
        string? rhymesTable = FindTable(schema, "RHYMES", "RHYME", "TEXTS", "WORK_TEXTS");
        string? membersTable = FindTable(schema, "CONCURSES_NAMES_MEMBERS", "CONCURS_NAMES_MEMBERS", "CONTEST_NAMES_MEMBERS", "MEMBERS");
        string? contestVotesTable = FindTable(schema, "CONCURS_VOTES", "CONTEST_VOTES", "VOTE_BLOCKS");
        string? votesTable = FindTable(schema, "VOTES", "VOTE");
        string? topicsTable = FindTable(schema, "CONCURS_TOPICS", "CONTEST_TOPICS", "TOPICS");

        if (contestsTable is null)
            throw new InvalidOperationException("В *.fdb не найдена таблица CONCURSES/CONTESTS. Это не похоже на старую базу конкурсов.");

        if (detailsTable is null)
            report.Warnings.Add("Не найдена таблица работ CONCURS_DTL - конкурсы будут импортированы без списка работ.");

        Dictionary<int, string> poets = poetsTable is null
            ? new Dictionary<int, string>()
            : ReadPoets(connection, poetsTable);

        Dictionary<int, string> topics = topicsTable is null
            ? new Dictionary<int, string>()
            : ReadTopics(connection, topicsTable);

        Dictionary<int, RhymeText> rhymes = rhymesTable is null
            ? new Dictionary<int, RhymeText>()
            : ReadRhymes(connection, rhymesTable);

        Dictionary<int, ContestNameRow> contestNames = contestNamesTable is null
            ? new Dictionary<int, ContestNameRow>()
            : ReadContestNames(connection, contestNamesTable);

        List<FbRow> contests = ReadRows(connection, contestsTable);
        List<FbRow> details = detailsTable is null ? new List<FbRow>() : ReadRows(connection, detailsTable);
        List<FbRow> members = membersTable is null ? new List<FbRow>() : ReadRows(connection, membersTable);
        List<FbRow> contestVotes = importVotes && contestVotesTable is not null ? ReadRows(connection, contestVotesTable) : new List<FbRow>();
        List<FbRow> votes = importVotes && votesTable is not null ? ReadRows(connection, votesTable) : new List<FbRow>();

        var detailsById = details
            .Select(x => new { Id = GetInt(x, "CD_ID", "CDID", "ID"), Row = x })
            .Where(x => x.Id != 0)
            .GroupBy(x => x.Id)
            .ToDictionary(x => x.Key, x => x.First().Row);

        var contestVotesById = contestVotes
            .Select(x => new { Id = GetInt(x, "CV_ID", "CVID", "ID"), Row = x })
            .Where(x => x.Id != 0)
            .GroupBy(x => x.Id)
            .ToDictionary(x => x.Key, x => x.First().Row);

        var contestIdMap = new Dictionary<int, string>();
        var contestByOldId = new Dictionary<int, Contest>();
        var contestNameIdByOldContestId = new Dictionary<int, int>();

        int fallbackNo = 1;
        foreach (FbRow contestRow in contests)
        {
            int oldContestId = GetInt(contestRow, "C_ID", "CID", "ID", "CONTEST_ID");
            if (oldContestId == 0)
                oldContestId = fallbackNo;

            int contestNameId = GetInt(contestRow, "C_CN_ID", "CCNID", "CN_ID", "CONTEST_NAME_ID");
            contestNames.TryGetValue(contestNameId, out ContestNameRow? nameRow);

            int number = GetInt(contestRow, "C_NUM", "CNUM", "NUM", "NUMBER", "NO");
            if (number == 0 && nameRow is not null)
                number = nameRow.Number;
            if (number == 0)
                number = fallbackNo;

            string name = FirstNonEmpty(
                GetString(contestRow, "C_NAME", "NAME", "TITLE"),
                nameRow?.Name,
                $"Импорт из Firebird #{number:000}");

            var contest = new Contest
            {
                Id = BuildImportedId(databasePath, oldContestId),
                Number = number.ToString("000"),
                Name = name,
                BaseVote = nameRow?.BaseVote ?? 3,
                MaxVote = nameRow?.MaxVote ?? 4,
                LimitMaxVote = nameRow?.LimitMaxVote ?? 0,
                VoteLimit = GetInt(contestRow, "C_VOTE_MAX", "CVOTEMAX", "VOTE_MAX"),
                OneMaxVotePerTopic = nameRow?.OneOnTopic ?? false,
                LimitMaxVoteByTopic = nameRow?.LimitMaxVoteByTopic ?? false,
                AllowZeroVotes = nameRow?.CanNullRate ?? false,
                TreatSelfVoteAsZero = !(nameRow?.CanVoteForSelf ?? false),
                HostKnowsAuthors = true,
                StartedAt = GetDateTime(contestRow, "C_DATE", "CDATE", "CREATED_AT") ?? DateTime.Now,
                ClosedAt = null,
                IsActive = true,
                CreatedAt = GetDateTime(contestRow, "C_DATE", "CDATE", "CREATED_AT") ?? DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            contestIdMap[oldContestId] = contest.Id;
            contestByOldId[oldContestId] = contest;
            contestNameIdByOldContestId[oldContestId] = contestNameId;
            report.Contests.Add(contest);
            fallbackNo++;
        }

        FillWorks(report, contestByOldId, details, poets, rhymes, topics);
        FillVoters(report, contestByOldId, contestNameIdByOldContestId, members, contestVotes, poets);

        if (importVotes)
            FillVotes(report, contestByOldId, contestIdMap, detailsById, contestVotesById, votes, poets);

        foreach (Contest contest in report.Contests)
        {
            contest.Works = contest.Works.OrderBy(x => x.Number).ThenBy(x => x.Title).ToList();
            contest.Voters = contest.Voters
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .GroupBy(x => NameNormalizer.Normalize(x.Name), StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .OrderBy(x => x.Name)
                .ToList();
        }

        if (report.Contests.Count == 0)
            report.Warnings.Add("Конкурсы не найдены: таблица есть, но строки не прочитаны.");

        return report;
    }

    private static FbConnection OpenBestConnection(string databasePath, string user, string password, out string mode)
    {
        user = string.IsNullOrWhiteSpace(user) ? "SYSDBA" : user.Trim();
        password = string.IsNullOrWhiteSpace(password) ? "masterkey" : password;

        string escapedPath = databasePath.Replace("'", "''");
        string[] attempts =
        {
            $"User={user};Password={password};Database={escapedPath};DataSource=localhost;Port=3050;Dialect=3;Charset=UTF8;",
            $"User={user};Password={password};Database={escapedPath};DataSource=localhost;Port=3050;Dialect=3;Charset=WIN1251;",
            $"User={user};Password={password};Database={escapedPath};Dialect=3;Charset=UTF8;ServerType=1;",
            $"User={user};Password={password};Database={escapedPath};Dialect=3;Charset=WIN1251;ServerType=1;"
        };

        var errors = new List<string>();
        for (int i = 0; i < attempts.Length; i++)
        {
            try
            {
                var connection = new FbConnection(attempts[i]);
                connection.Open();
                mode = i switch
                {
                    0 => "Firebird server / UTF8",
                    1 => "Firebird server / WIN1251",
                    2 => "Firebird embedded / UTF8",
                    _ => "Firebird embedded / WIN1251"
                };
                return connection;
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
            }
        }

        throw new InvalidOperationException(
            "Не удалось открыть *.fdb. Проверь, что Firebird Server запущен или рядом доступен embedded fbclient.dll. " +
            "Также проверь логин/пароль. Последняя ошибка: " + errors.LastOrDefault());
    }

    private static Dictionary<string, List<string>> ReadSchema(FbConnection connection)
    {
        var schema = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TRIM(r.RDB$RELATION_NAME) AS TABLE_NAME,
                   TRIM(f.RDB$FIELD_NAME) AS FIELD_NAME
            FROM RDB$RELATIONS r
            JOIN RDB$RELATION_FIELDS f ON f.RDB$RELATION_NAME = r.RDB$RELATION_NAME
            WHERE COALESCE(r.RDB$SYSTEM_FLAG, 0) = 0
              AND r.RDB$VIEW_BLR IS NULL
            ORDER BY r.RDB$RELATION_NAME, f.RDB$FIELD_POSITION
            """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            string table = reader.IsDBNull(0) ? string.Empty : reader.GetString(0).Trim();
            string field = reader.IsDBNull(1) ? string.Empty : reader.GetString(1).Trim();
            if (string.IsNullOrWhiteSpace(table) || string.IsNullOrWhiteSpace(field))
                continue;
            if (!schema.TryGetValue(table, out List<string>? fields))
            {
                fields = new List<string>();
                schema.Add(table, fields);
            }
            fields.Add(field);
        }

        return schema;
    }

    private static string? FindTable(Dictionary<string, List<string>> schema, params string[] names)
    {
        foreach (string name in names)
        {
            string? exact = schema.Keys.FirstOrDefault(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
                return exact;
        }

        foreach (string name in names)
        {
            string? contains = schema.Keys.FirstOrDefault(x => x.Contains(name, StringComparison.OrdinalIgnoreCase));
            if (contains is not null)
                return contains;
        }

        return null;
    }

    private static List<FbRow> ReadRows(FbConnection connection, string tableName)
    {
        var rows = new List<FbRow>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM " + QuoteIdentifier(tableName);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var row = new FbRow();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                object? value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                row[reader.GetName(i).Trim()] = value;
            }
            rows.Add(row);
        }
        return rows;
    }

    private static Dictionary<int, string> ReadPoets(FbConnection connection, string tableName)
    {
        return ReadRows(connection, tableName)
            .Select(row => new
            {
                Id = GetInt(row, "P_ID", "PID", "POET_ID", "ID"),
                Name = FirstNonEmpty(
                    GetString(row, "P_NAME", "PNAME", "NAME", "AUTHOR", "USER_NAME"),
                    GetString(row, "P_NICKNAME", "PNICKNAME", "NICKNAME"),
                    GetString(row, "P_NAME_ID", "PNAMEID", "LOGIN"))
            })
            .Where(x => x.Id != 0 && !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.Id)
            .ToDictionary(x => x.Key, x => x.First().Name);
    }

    private static Dictionary<int, string> ReadTopics(FbConnection connection, string tableName)
    {
        return ReadRows(connection, tableName)
            .Select(row => new
            {
                Id = GetInt(row, "CT_ID", "CTID", "TOPIC_ID", "ID"),
                Name = FirstNonEmpty(GetString(row, "CT_NAME", "CTNAME", "NAME", "TITLE", "TOPIC"))
            })
            .Where(x => x.Id != 0 && !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.Id)
            .ToDictionary(x => x.Key, x => x.First().Name);
    }

    private static Dictionary<int, RhymeText> ReadRhymes(FbConnection connection, string tableName)
    {
        return ReadRows(connection, tableName)
            .Select(row => new RhymeText(
                GetInt(row, "R_ID", "RID", "RHYME_ID", "ID"),
                FirstNonEmpty(GetString(row, "R_CONTENT", "RCONTENT", "CONTENT", "TEXT", "BODY")),
                FirstNonEmpty(GetString(row, "R_TITLE", "RTITLE", "TITLE", "NAME"))))
            .Where(x => x.Id != 0)
            .GroupBy(x => x.Id)
            .ToDictionary(x => x.Key, x => x.First());
    }

    private static Dictionary<int, ContestNameRow> ReadContestNames(FbConnection connection, string tableName)
    {
        return ReadRows(connection, tableName)
            .Select(row => new ContestNameRow
            {
                Id = GetInt(row, "CN_ID", "CNID", "ID"),
                Name = FirstNonEmpty(GetString(row, "CN_NAME", "CNNAME", "NAME", "TITLE")),
                Number = GetInt(row, "CN_NUM_GEN", "CNNUMGEN", "NUM", "NUMBER"),
                BaseVote = GetInt(row, "CN_BASE_VOTE", "CNBASEVOTE", "BASE_VOTE"),
                MaxVote = GetInt(row, "CN_MAX_VOTE", "CNMAXVOTE", "MAX_VOTE"),
                LimitMaxVote = GetInt(row, "CN_LIMIT_MAX_VOTE", "CNLIMITMAXVOTE", "LIMIT_MAX_VOTE"),
                OneOnTopic = GetBool(row, "CN_ONE_ON_TOPIC", "CNONEONTOPIC", "ONE_ON_TOPIC"),
                LimitMaxVoteByTopic = GetBool(row, "CN_LIMIT_MAX_VOTE_BY_TOPIC", "CNLIMITMAXVOTEBYTOPIC", "LIMIT_MAX_VOTE_BY_TOPIC"),
                CanNullRate = GetBool(row, "CN_CAN_NULL_RATE", "CNCANNULLRATE", "CAN_NULL_RATE"),
                CanVoteForSelf = GetBool(row, "CN_CAN_VOTE_FOR_SELF", "CNCANVOTEFORSELF", "CAN_VOTE_FOR_SELF")
            })
            .Where(x => x.Id != 0)
            .GroupBy(x => x.Id)
            .ToDictionary(x => x.Key, x => x.First());
    }

    private static void FillWorks(
        FirebirdImportReport report,
        Dictionary<int, Contest> contestByOldId,
        List<FbRow> details,
        Dictionary<int, string> poets,
        Dictionary<int, RhymeText> rhymes,
        Dictionary<int, string> topics)
    {
        foreach (FbRow detail in details)
        {
            int oldContestId = GetInt(detail, "CD_C_ID", "CDCID", "C_ID", "CONTEST_ID");
            if (!contestByOldId.TryGetValue(oldContestId, out Contest? contest))
                continue;

            int no = GetInt(detail, "CD_R_NO", "CDRNO", "R_NO", "WORK_NO", "NUMBER", "NO");
            if (no <= 0)
                no = contest.Works.Count + 1;

            int poetId = GetInt(detail, "CD_P_ID", "CDPID", "P_ID", "AUTHOR_ID");
            int rhymeId = GetInt(detail, "CD_R_ID", "CDRID", "R_ID", "RHYME_ID");
            int topicId = GetInt(detail, "CD_CT_ID", "CDCTID", "CT_ID", "TOPIC_ID");
            rhymes.TryGetValue(rhymeId, out RhymeText? rhyme);

            string content = FirstNonEmpty(
                GetString(detail, "CD_CONTENT", "CONTENT", "TEXT", "BODY"),
                rhyme?.Content);
            string title = FirstNonEmpty(
                GetString(detail, "CD_TITLE", "CD_NAME", "TITLE", "NAME"),
                rhyme?.Title,
                FirstContentLine(content),
                $"Работа №{no:00}");
            string author = poets.TryGetValue(poetId, out string? poetName) ? poetName : string.Empty;
            string topic = FirstNonEmpty(GetString(detail, "CD_TOPIC", "TOPIC"), topics.TryGetValue(topicId, out string? topicName) ? topicName : string.Empty);

            contest.Works.Add(new ContestWork
            {
                Number = no,
                Title = title,
                Author = author,
                Topic = topic,
                Content = content
            });
        }
    }

    private static void FillVoters(
        FirebirdImportReport report,
        Dictionary<int, Contest> contestByOldId,
        Dictionary<int, int> contestNameIdByOldContestId,
        List<FbRow> members,
        List<FbRow> contestVotes,
        Dictionary<int, string> poets)
    {
        var contestsByNameId = contestNameIdByOldContestId
            .GroupBy(x => x.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => contestByOldId[y.Key]).ToList());

        foreach (var pair in contestByOldId)
        {
            foreach (string author in pair.Value.Works.Select(x => x.Author).Where(x => !string.IsNullOrWhiteSpace(x)))
                AddVoter(pair.Value, author);
        }

        foreach (FbRow member in members)
        {
            int cnId = GetInt(member, "CNM_CN_ID", "CNMCNID", "CN_ID", "CONTEST_NAME_ID");
            int poetId = GetInt(member, "CNM_P_ID", "CNMPID", "P_ID", "POET_ID");
            if (!poets.TryGetValue(poetId, out string? poetName))
                continue;

            if (!contestsByNameId.TryGetValue(cnId, out List<Contest>? contestsForName))
                continue;

            foreach (Contest contest in contestsForName)
                AddVoter(contest, poetName);
        }

        foreach (FbRow cv in contestVotes)
        {
            int oldContestId = GetInt(cv, "CV_C_ID", "CVCID", "C_ID", "CONTEST_ID");
            int poetId = GetInt(cv, "CV_P_ID", "CVPID", "P_ID", "POET_ID", "VOTER_ID");
            if (!contestByOldId.TryGetValue(oldContestId, out Contest? contest))
                continue;
            if (poets.TryGetValue(poetId, out string? poetName))
                AddVoter(contest, poetName);
        }
    }

    private static void FillVotes(
        FirebirdImportReport report,
        Dictionary<int, Contest> contestByOldId,
        Dictionary<int, string> contestIdMap,
        Dictionary<int, FbRow> detailsById,
        Dictionary<int, FbRow> contestVotesById,
        List<FbRow> votes,
        Dictionary<int, string> poets)
    {
        foreach (FbRow voteRow in votes)
        {
            int cvId = GetInt(voteRow, "V_CV_ID", "VCVID", "CV_ID", "CONTEST_VOTE_ID");
            contestVotesById.TryGetValue(cvId, out FbRow? cvRow);

            int detailId = GetInt(voteRow, "V_CD_ID", "VCDID", "CD_ID", "DETAIL_ID");
            detailsById.TryGetValue(detailId, out FbRow? detailRow);

            int oldContestId = FirstNonZero(
                GetInt(voteRow, "V_C_ID", "VCID", "C_ID", "CONTEST_ID"),
                cvRow is null ? 0 : GetInt(cvRow, "CV_C_ID", "CVCID", "C_ID", "CONTEST_ID"),
                detailRow is null ? 0 : GetInt(detailRow, "CD_C_ID", "CDCID", "C_ID", "CONTEST_ID"));

            if (!contestIdMap.TryGetValue(oldContestId, out string? newContestId))
                continue;

            int voterId = FirstNonZero(
                GetInt(voteRow, "V_P_ID", "VPID", "P_ID", "VOTER_ID"),
                cvRow is null ? 0 : GetInt(cvRow, "CV_P_ID", "CVPID", "P_ID", "POET_ID", "VOTER_ID"));
            string voterName = poets.TryGetValue(voterId, out string? name) ? name : $"Голосующий #{voterId}";

            int workNo = FirstNonZero(
                GetInt(voteRow, "V_CR_NO", "VCRNO", "R_NO", "WORK_NO", "NUMBER", "NO"),
                detailRow is null ? 0 : GetInt(detailRow, "CD_R_NO", "CDRNO", "R_NO", "WORK_NO", "NUMBER", "NO"));
            if (workNo <= 0)
                continue;

            int originalRate = FirstNonZero(GetInt(voteRow, "V_P_RATE", "VPRATE", "P_RATE", "ORIGINAL_RATE"), GetInt(voteRow, "V_RATE", "VRATE", "RATE", "SCORE"));
            int rate = FirstNonZero(GetInt(voteRow, "V_RATE", "VRATE", "RATE", "SCORE"), originalRate);
            string comment = FirstNonEmpty(GetString(voteRow, "V_COMMENT", "COMMENT", "NOTE", "V_NOTE"));

            var entry = new VoteEntry
            {
                ContestId = newContestId,
                VoterName = voterName,
                VoterKey = NameNormalizer.Normalize(voterName),
                WorkNo = workNo,
                Score = rate,
                ScoreText = rate.ToString(),
                OriginalScore = originalRate,
                OriginalScoreText = originalRate == 0 ? rate.ToString() : originalRate.ToString(),
                VotedScore = originalRate == 0 ? rate : originalRate,
                VotedScoreText = originalRate == 0 ? rate.ToString() : originalRate.ToString(),
                AcceptedScore = rate,
                AcceptedScoreText = rate.ToString(),
                WasChangedByRules = false,
                RuleNote = string.Empty,
                Comment = comment,
                SourceLine = $"№{workNo:00} - {rate} (импорт Firebird)",
                UpdatedAt = cvRow is null ? DateTime.Now : GetDateTime(cvRow, "CV_TIMESTAMP", "CV_DATE", "UPDATED_AT") ?? DateTime.Now
            };

            if (!report.VotesByContestId.TryGetValue(newContestId, out List<VoteEntry>? list))
            {
                list = new List<VoteEntry>();
                report.VotesByContestId.Add(newContestId, list);
            }
            list.Add(entry);

            if (contestByOldId.TryGetValue(oldContestId, out Contest? contest))
                AddVoter(contest, voterName);
        }
    }

    private static void AddVoter(Contest contest, string voterName)
    {
        voterName = (voterName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(voterName))
            return;
        string key = NameNormalizer.Normalize(voterName);
        if (contest.Voters.Any(x => NameNormalizer.Normalize(x.Name).Equals(key, StringComparison.OrdinalIgnoreCase)))
            return;
        contest.Voters.Add(new VoterSetting { Name = voterName, MustVote = true });
    }

    private static string BuildImportedId(string databasePath, int oldContestId)
    {
        string source = Path.GetFullPath(databasePath).ToLowerInvariant() + ":" + oldContestId.ToString();
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        return "fdb_" + Convert.ToHexString(hash).ToLowerInvariant()[..20];
    }

    private static string QuoteIdentifier(string identifier)
    {
        return "\"" + identifier.Replace("\"", "\"\"") + "\"";
    }

    private static int GetInt(FbRow row, params string[] names)
    {
        object? value = GetValue(row, names);
        if (value is null)
            return 0;
        try
        {
            return value switch
            {
                int i => i,
                long l => (int)l,
                short s => s,
                decimal d => (int)d,
                double d => (int)d,
                float f => (int)f,
                bool b => b ? 1 : 0,
                string s when int.TryParse(s.Trim(), out int parsed) => parsed,
                _ => Convert.ToInt32(value)
            };
        }
        catch
        {
            return 0;
        }
    }

    private static bool GetBool(FbRow row, params string[] names)
    {
        object? value = GetValue(row, names);
        if (value is null)
            return false;
        if (value is bool b)
            return b;
        if (value is string s)
            return s.Equals("true", StringComparison.OrdinalIgnoreCase) || s.Equals("1") || s.Equals("Y", StringComparison.OrdinalIgnoreCase);
        try
        {
            return Convert.ToInt32(value) != 0;
        }
        catch
        {
            return false;
        }
    }

    private static DateTime? GetDateTime(FbRow row, params string[] names)
    {
        object? value = GetValue(row, names);
        if (value is null)
            return null;
        if (value is DateTime dt)
            return dt;
        return DateTime.TryParse(value.ToString(), out DateTime parsed) ? parsed : null;
    }

    private static string GetString(FbRow row, params string[] names)
    {
        object? value = GetValue(row, names);
        if (value is null)
            return string.Empty;
        if (value is byte[] bytes)
            return Encoding.UTF8.GetString(bytes).Trim('\0', ' ', '\r', '\n');
        return value.ToString()?.Trim() ?? string.Empty;
    }

    private static object? GetValue(FbRow row, params string[] names)
    {
        foreach (string name in names)
        {
            if (row.TryGetValue(name, out object? value))
                return value;
        }
        return null;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (string? value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }
        return string.Empty;
    }

    private static int FirstNonZero(params int[] values)
    {
        foreach (int value in values)
        {
            if (value != 0)
                return value;
        }
        return 0;
    }

    private static string FirstContentLine(string content)
    {
        foreach (string line in (content ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            string trimmed = line.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed) && trimmed != ".")
                return trimmed;
        }
        return string.Empty;
    }

    private sealed class FbRow : Dictionary<string, object?>
    {
        public FbRow() : base(StringComparer.OrdinalIgnoreCase)
        {
        }
    }

    private sealed record RhymeText(int Id, string Content, string Title);

    private sealed class ContestNameRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Number { get; set; }
        public int BaseVote { get; set; } = 3;
        public int MaxVote { get; set; } = 4;
        public int LimitMaxVote { get; set; }
        public bool OneOnTopic { get; set; }
        public bool LimitMaxVoteByTopic { get; set; }
        public bool CanNullRate { get; set; }
        public bool CanVoteForSelf { get; set; }
    }
}
