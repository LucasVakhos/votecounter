using VoteCounter.Core.Services;
using System.Globalization;
using System.Text;
using VoteCounter.Core.Models;

namespace VoteCounter.Data.Database;

/// <summary>
/// Сохраняет отчёт авточека правил перед импортом голосов:
/// кто будет добавлен, кто будет заменён, какие оценки изменены правилами.
/// Рабочую базу не трогает - только пишет диагностические файлы рядом с Database/Reports.
/// </summary>
public sealed class VoteImportReportService
{
    public string SavePreview(Contest contest, ImportResult result, IReadOnlyList<VoteEntry> existingVotes)
    {
        if (contest is null)
            throw new ArgumentNullException(nameof(contest));

        if (result is null)
            throw new ArgumentNullException(nameof(result));

        string folder = BuildReportFolder(contest);
        Directory.CreateDirectory(folder);

        File.WriteAllText(Path.Combine(folder, "auto_rule_check.txt"), BuildSummary(contest, result, existingVotes), Encoding.UTF8);
        File.WriteAllText(Path.Combine(folder, "incoming_votes.csv"), BuildVotesCsv(result), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        File.WriteAllText(Path.Combine(folder, "replaced_voters.csv"), BuildReplacedVotersCsv(result, existingVotes), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        File.WriteAllText(Path.Combine(folder, "warnings.txt"), BuildWarnings(result), Encoding.UTF8);

        return folder;
    }

    public VoteImportChangeSummary BuildChangeSummary(ImportResult result, IReadOnlyList<VoteEntry> existingVotes)
    {
        var existingByVoter = existingVotes
            .Where(x => !string.IsNullOrWhiteSpace(x.VoterKey) || !string.IsNullOrWhiteSpace(x.VoterName))
            .GroupBy(x => string.IsNullOrWhiteSpace(x.VoterKey) ? NameNormalizer.Normalize(x.VoterName) : x.VoterKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var incomingKeys = result.Blocks
            .Select(x => NameNormalizer.Normalize(x.VoterName))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        int replacedVoters = incomingKeys.Count(existingByVoter.ContainsKey);
        int newVoters = incomingKeys.Count - replacedVoters;
        int replacedVotes = incomingKeys
            .Where(existingByVoter.ContainsKey)
            .Sum(x => existingByVoter[x].Count);
        int changedByRules = result.Blocks.SelectMany(x => x.Votes).Count(x => x.WasChangedByRules);
        int selfVotes = result.Blocks.SelectMany(x => x.Votes).Count(x => x.RuleNote.Contains("самоголос", StringComparison.OrdinalIgnoreCase));

        return new VoteImportChangeSummary(
            NewVoters: newVoters,
            ReplacedVoters: replacedVoters,
            ReplacedVotes: replacedVotes,
            IncomingVotes: result.VoteCount,
            ChangedByRules: changedByRules,
            SelfVotesToZero: selfVotes,
            WarningCount: result.Warnings.Count);
    }

    private static string BuildReportFolder(Contest contest)
    {
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        string safeNo = SanitizeFilePart(string.IsNullOrWhiteSpace(contest.Number) ? "contest" : contest.Number);
        return Path.Combine(LocalDatabase.DatabaseFolder, "Reports", $"{stamp}_vote_import_{safeNo}");
    }

    private string BuildSummary(Contest contest, ImportResult result, IReadOnlyList<VoteEntry> existingVotes)
    {
        VoteImportChangeSummary changes = BuildChangeSummary(result, existingVotes);
        var sb = new StringBuilder();
        sb.AppendLine("VoteCounter - отчёт авточека правил импорта голосов");
        sb.AppendLine(new string('=', 58));
        sb.AppendLine($"Дата: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
        sb.AppendLine($"Конкурс: №{contest.Number} - {contest.Name}");
        sb.AppendLine($"Работ в настройках: {contest.Works.Count}");
        sb.AppendLine($"Голосующих в настройках: {contest.Voters.Count}");
        sb.AppendLine();
        sb.AppendLine("Что будет записано после авточека:");
        sb.AppendLine($"- новых голосующих: {changes.NewVoters}");
        sb.AppendLine($"- голосующих с заменой старых голосов: {changes.ReplacedVoters}");
        sb.AppendLine($"- старых голосов будет заменено: {changes.ReplacedVotes}");
        sb.AppendLine($"- входящих голосов будет записано: {changes.IncomingVotes}");
        sb.AppendLine($"- автоматически принято иначе по правилам: {changes.ChangedByRules}");
        sb.AppendLine($"- самоголосов в 0: {changes.SelfVotesToZero}");
        sb.AppendLine($"- предупреждений: {changes.WarningCount}");
        sb.AppendLine();
        sb.AppendLine("Правила конкурса:");
        sb.AppendLine($"- MaxVote: {contest.MaxVote}");
        sb.AppendLine($"- BaseVote: {contest.BaseVote}");
        sb.AppendLine($"- LimitMaxVote: {contest.LimitMaxVote}");
        sb.AppendLine($"- OneMaxVotePerTopic: {contest.OneMaxVotePerTopic}");
        sb.AppendLine($"- LimitMaxVoteByTopic: {contest.LimitMaxVoteByTopic}");
        sb.AppendLine($"- Extra max auto-accept as base: {contest.DowngradeExtraMaxVoteToBase}");
        sb.AppendLine($"- AllowZeroVotes: {contest.AllowZeroVotes}");
        sb.AppendLine($"- SelfVoteAsZero: {contest.TreatSelfVoteAsZero}");
        sb.AppendLine();
        sb.AppendLine("Голосующие:");
        foreach (ParsedVoteBlock block in result.Blocks.OrderBy(x => x.VoterName))
            sb.AppendLine($"- {block.VoterName}: {block.Votes.Count} голосов");

        if (result.Warnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Предупреждения:");
            foreach (string warning in result.Warnings)
                sb.AppendLine("- " + warning);
        }

        return sb.ToString();
    }

    private static string BuildVotesCsv(ImportResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("VoterName;WorkNo;AcceptedScore;AcceptedScoreText;VotedScore;VotedScoreText;ChangedByRules;RuleNote;Comment;SourceLine");
        foreach (VoteEntry vote in result.Blocks.SelectMany(x => x.Votes).OrderBy(x => x.VoterName).ThenBy(x => x.WorkNo))
        {
            sb.Append(EscapeCsv(vote.VoterName)).Append(';');
            sb.Append(vote.WorkNo.ToString(CultureInfo.InvariantCulture)).Append(';');
            decimal acceptedScore = vote.AcceptedScore != 0m || !string.IsNullOrWhiteSpace(vote.AcceptedScoreText) ? vote.AcceptedScore : vote.Score;
            string acceptedText = string.IsNullOrWhiteSpace(vote.AcceptedScoreText) ? vote.ScoreText : vote.AcceptedScoreText;
            decimal votedScore = vote.VotedScore != 0m || !string.IsNullOrWhiteSpace(vote.VotedScoreText) ? vote.VotedScore : vote.OriginalScore;
            string votedText = string.IsNullOrWhiteSpace(vote.VotedScoreText) ? vote.OriginalScoreText : vote.VotedScoreText;
            sb.Append(acceptedScore.ToString("0.##", CultureInfo.InvariantCulture)).Append(';');
            sb.Append(EscapeCsv(acceptedText)).Append(';');
            sb.Append(votedScore.ToString("0.##", CultureInfo.InvariantCulture)).Append(';');
            sb.Append(EscapeCsv(votedText)).Append(';');
            sb.Append(vote.WasChangedByRules ? "1" : "0").Append(';');
            sb.Append(EscapeCsv(vote.RuleNote)).Append(';');
            sb.Append(EscapeCsv(vote.Comment)).Append(';');
            sb.AppendLine(EscapeCsv(vote.SourceLine));
        }

        return sb.ToString();
    }

    private static string BuildReplacedVotersCsv(ImportResult result, IReadOnlyList<VoteEntry> existingVotes)
    {
        var existingByVoter = existingVotes
            .GroupBy(x => string.IsNullOrWhiteSpace(x.VoterKey) ? NameNormalizer.Normalize(x.VoterName) : x.VoterKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var sb = new StringBuilder();
        sb.AppendLine("VoterName;Mode;OldVoteCount;NewVoteCount");
        foreach (ParsedVoteBlock block in result.Blocks.OrderBy(x => x.VoterName))
        {
            string key = NameNormalizer.Normalize(block.VoterName);
            bool exists = existingByVoter.TryGetValue(key, out List<VoteEntry>? oldVotes);
            sb.Append(EscapeCsv(block.VoterName)).Append(';');
            sb.Append(exists ? "replace" : "new").Append(';');
            sb.Append((oldVotes?.Count ?? 0).ToString(CultureInfo.InvariantCulture)).Append(';');
            sb.AppendLine(block.Votes.Count.ToString(CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    private static string BuildWarnings(ImportResult result)
    {
        if (result.Warnings.Count == 0)
            return "Предупреждений нет." + Environment.NewLine;

        var sb = new StringBuilder();
        foreach (string warning in result.Warnings)
            sb.AppendLine(warning);
        return sb.ToString();
    }

    private static string EscapeCsv(string? value)
    {
        string text = value ?? string.Empty;
        if (text.Contains(';') || text.Contains('"') || text.Contains('\n') || text.Contains('\r'))
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        return text;
    }

    private static string SanitizeFilePart(string value)
    {
        var chars = value.Select(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' ? ch : '_').ToArray();
        string result = new string(chars).Trim('_');
        return string.IsNullOrWhiteSpace(result) ? "contest" : result;
    }
}

public sealed record VoteImportChangeSummary(
    int NewVoters,
    int ReplacedVoters,
    int ReplacedVotes,
    int IncomingVotes,
    int ChangedByRules,
    int SelfVotesToZero,
    int WarningCount);
