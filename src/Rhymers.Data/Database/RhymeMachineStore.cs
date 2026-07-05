using Rhymers.Core.Services;
using Rhymers.Core.Models;

namespace Rhymers.Data.Database;

/// <summary>
/// Локальное зеркало старой базы Rhyme Machine в SQLite.
/// Нужно не вместо Excel, а как правильное ядро хранения: конкурсы, авторы, работы,
/// пакеты голосования, отдельные оценки, правила и места.
/// </summary>
public sealed class RhymeMachineStore
{
    public string RootFolder => LocalDatabase.DatabaseFolder;
    public string DatabaseFile => LocalDatabase.DatabasePath;

    public RhymeMachineStore()
    {
        LocalDatabase.Initialize();
    }

    public RhymeMachineDb Load()
    {
        // STAGE15: рабочее хранение стало полностью реляционным в Database/VoteCounter.db.
        // Старое JSON-зеркало больше не используется. Метод оставлен только для совместимости
        // с уже написанными командами UI.
        return new RhymeMachineDb();
    }

    public void Save(RhymeMachineDb db)
    {
        // Больше не пишем JSON-зеркало. Основные таблицы Contests/ContestWorks/VoterSettings/Votes
        // сохраняются через LocalStore/LocalDatabase.
    }

    public string Sync(Contest contest, IEnumerable<VoteEntry> allVotes)
    {
        return DatabaseFile;
    }

    public void Sync(RhymeMachineDb db, Contest contest, IEnumerable<VoteEntry> allVotes)
    {
        List<VoteEntry> votes = allVotes
            .GroupBy(x => (x.VoterKey, x.WorkNo))
            .Select(g => g.Last())
            .OrderBy(x => x.VoterName)
            .ThenBy(x => x.WorkNo)
            .ToList();

        RmContestName cn = UpsertContestName(db, contest);
        RmVoteRule rule = UpsertVoteRule(db, contest, cn.CnId);
        UpsertVoteValues(db, rule.CnvrId, contest);

        RmContest c = UpsertContest(db, contest, cn.CnId, votes);
        cn.CnCId = c.CId;

        Dictionary<string, RmPoet> poetsByKey = UpsertPoets(db, contest, votes);
        UpsertMembers(db, contest, cn.CnId, poetsByKey);
        Dictionary<int, RmContestDetail> detailsByWorkNo = UpsertContestDetails(db, contest, c.CId, poetsByKey);
        UpsertContestVotesAndVotes(db, contest, c.CId, votes, poetsByKey, detailsByWorkNo);
        RecalculateTotals(db, c.CId);
        RebuildWinners(db, c.CId);
    }

    private static RmContestName UpsertContestName(RhymeMachineDb db, Contest contest)
    {
        RmContestName? existing = db.ContestNames.FirstOrDefault(x => x.SourceContestId == contest.Id);
        if (existing is null)
        {
            existing = new RmContestName
            {
                CnId = db.NextContestNameId++,
                SourceContestId = contest.Id
            };
            db.ContestNames.Add(existing);
        }

        existing.CnName = contest.Name;
        existing.CnNumGen = TryParseNumber(contest.Number);
        existing.CnMaxVote = contest.MaxVote;
        existing.CnBaseVote = contest.BaseVote;
        existing.CnLimitMaxVote = contest.LimitMaxVote > 0 ? contest.LimitMaxVote : contest.VoteLimit;
        existing.CnOneOnTopic = contest.OneMaxVotePerTopic;
        existing.CnLimitMaxVoteByTopic = contest.LimitMaxVoteByTopic;
        existing.CnCanNullRate = contest.AllowZeroVotes;
        existing.CnCanVoteForSelf = !contest.TreatSelfVoteAsZero;
        return existing;
    }

    private static RmVoteRule UpsertVoteRule(RhymeMachineDb db, Contest contest, int cnId)
    {
        RmVoteRule? rule = db.VoteRules.FirstOrDefault(x => x.CnwrCnId == cnId);
        if (rule is null)
        {
            rule = new RmVoteRule { CnvrId = db.NextVoteRuleId++, CnwrCnId = cnId };
            db.VoteRules.Add(rule);
        }

        rule.CnwrBaseVote = contest.BaseVote;
        rule.CnwrMaxVote = contest.MaxVote;
        rule.CnwrLimitMaxVote = contest.LimitMaxVote > 0 ? contest.LimitMaxVote : contest.VoteLimit;
        rule.CnwrOneOnTopic = contest.OneMaxVotePerTopic;
        rule.CnwrLimitMaxVoteByTopic = contest.LimitMaxVoteByTopic;
        rule.CnwrCanNullRate = contest.AllowZeroVotes;
        rule.CnwrCanVoteForSelf = !contest.TreatSelfVoteAsZero;
        return rule;
    }

    private static void UpsertVoteValues(RhymeMachineDb db, int ruleId, Contest contest)
    {
        db.VoteValues.RemoveAll(x => x.CnvvCnvrId == ruleId);
        int min = contest.AllowZeroVotes ? 0 : 1;
        for (int value = min; value <= Math.Max(contest.MaxVote, contest.BaseVote); value++)
        {
            db.VoteValues.Add(new RmVoteValue
            {
                CnvvId = db.NextVoteValueId++,
                CnvvCnvrId = ruleId,
                CnvvValue = value,
                CnvvQty = 0,
                CnvvNeedNote = false
            });
        }
    }

    private static RmContest UpsertContest(RhymeMachineDb db, Contest contest, int cnId, IReadOnlyCollection<VoteEntry> votes)
    {
        RmContest? existing = db.Contests.FirstOrDefault(x => x.SourceContestId == contest.Id);
        if (existing is null)
        {
            existing = new RmContest
            {
                CId = db.NextContestId++,
                SourceContestId = contest.Id
            };
            db.Contests.Add(existing);
        }

        existing.CCnId = cnId;
        existing.CNum = TryParseNumber(contest.Number);
        existing.CDtl = contest.Works.Count(x => x.Number > 0);
        existing.CPoets = contest.Works
            .Select(x => NameNormalizer.Normalize(x.Author))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        existing.CVoteMax = contest.VoteLimit;
        existing.CVotes = votes.Select(x => x.VoterKey).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        existing.COpened = true;
        existing.CReceptClosed = true;
        existing.CVoteClosed = false;
        existing.CClosed = false;
        existing.CStadia = 2;
        return existing;
    }

    private static Dictionary<string, RmPoet> UpsertPoets(RhymeMachineDb db, Contest contest, IEnumerable<VoteEntry> votes)
    {
        var names = new List<string>();
        names.AddRange(contest.Works.Select(x => x.Author));
        names.AddRange(contest.Voters.Select(x => x.Name));
        names.AddRange(votes.Select(x => x.VoterName));

        foreach (string rawName in names)
        {
            string name = (rawName ?? string.Empty).Trim();
            string key = NameNormalizer.Normalize(name);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            RmPoet? poet = db.Poets.FirstOrDefault(x => x.PNameId.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (poet is null)
            {
                poet = new RmPoet
                {
                    PId = db.NextPoetId++,
                    PNameId = key,
                    PName = name,
                    PUrl = "local://" + key,
                    PNotify = true,
                    PIsPoet = true
                };
                db.Poets.Add(poet);
            }
            else if (!string.IsNullOrWhiteSpace(name))
            {
                poet.PName = name;
            }
        }

        return db.Poets.ToDictionary(x => x.PNameId, x => x, StringComparer.OrdinalIgnoreCase);
    }

    private static void UpsertMembers(RhymeMachineDb db, Contest contest, int cnId, IReadOnlyDictionary<string, RmPoet> poetsByKey)
    {
        var authorKeys = contest.Works
            .Select(x => NameNormalizer.Normalize(x.Author))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var voterKeys = contest.Voters
            .Select(x => NameNormalizer.Normalize(x.Name))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (string key in authorKeys.Concat(voterKeys).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!poetsByKey.TryGetValue(key, out RmPoet? poet))
                continue;

            RmContestNameMember? member = db.ContestNameMembers.FirstOrDefault(x => x.CnmCnId == cnId && x.CnmPId == poet.PId);
            if (member is null)
            {
                member = new RmContestNameMember
                {
                    CnmId = db.NextContestNameMemberId++,
                    CnmCnId = cnId,
                    CnmPId = poet.PId
                };
                db.ContestNameMembers.Add(member);
            }

            member.CnmIsPoet = authorKeys.Contains(key);
            member.CnmActive = true;
            member.CnmCd = contest.Works.Count(x => NameNormalizer.Same(x.Author, poet.PName));
        }
    }

    private static Dictionary<int, RmContestDetail> UpsertContestDetails(
        RhymeMachineDb db,
        Contest contest,
        int contestId,
        IReadOnlyDictionary<string, RmPoet> poetsByKey)
    {
        db.ContestDetails.RemoveAll(x => x.CdCId == contestId);
        var result = new Dictionary<int, RmContestDetail>();

        foreach (ContestWork work in contest.Works.Where(x => x.Number > 0).OrderBy(x => x.Number))
        {
            string authorKey = NameNormalizer.Normalize(work.Author);
            if (!poetsByKey.TryGetValue(authorKey, out RmPoet? poet))
                continue;

            var rhyme = new RmRhyme
            {
                RId = db.NextRhymeId++,
                RPId = poet.PId,
                RContent = string.IsNullOrWhiteSpace(work.Content) ? (work.Title ?? string.Empty) : work.Content,
                RUrl = string.Empty
            };
            db.Rhymes.Add(rhyme);

            var detail = new RmContestDetail
            {
                CdId = db.NextContestDetailId++,
                CdCId = contestId,
                CdRNo = work.Number,
                CdPId = poet.PId,
                CdRId = rhyme.RId,
                CdRate = 0,
                Title = work.Title ?? string.Empty,
                Topic = work.Topic ?? string.Empty,
                CdClosed = false,
                CdOutOfContest = false
            };

            db.ContestDetails.Add(detail);
            result[work.Number] = detail;
        }

        return result;
    }

    private static void UpsertContestVotesAndVotes(
        RhymeMachineDb db,
        Contest contest,
        int contestId,
        IEnumerable<VoteEntry> votes,
        IReadOnlyDictionary<string, RmPoet> poetsByKey,
        IReadOnlyDictionary<int, RmContestDetail> detailsByWorkNo)
    {
        db.ContestVotes.RemoveAll(x => x.CvCId == contestId);
        db.Votes.RemoveAll(x => x.VCId == contestId);

        foreach (IGrouping<string, VoteEntry> group in votes
                     .Where(x => !string.IsNullOrWhiteSpace(x.VoterKey))
                     .GroupBy(x => x.VoterKey, StringComparer.OrdinalIgnoreCase))
        {
            if (!poetsByKey.TryGetValue(group.Key, out RmPoet? voter))
                continue;

            var voterVotes = group
                .GroupBy(x => x.WorkNo)
                .Select(g => g.Last())
                .OrderBy(x => x.WorkNo)
                .ToList();

            var cv = new RmContestVote
            {
                CvId = db.NextContestVoteId++,
                CvCId = contestId,
                CvPId = voter.PId,
                CvTimestamp = voterVotes.Count == 0 ? DateTime.Now : voterVotes.Max(x => x.UpdatedAt),
                CvOriginal = string.Join(Environment.NewLine, voterVotes.Select(x => x.SourceLine).Where(x => !string.IsNullOrWhiteSpace(x))),
                CvVoted = voterVotes.Count > 0,
                CvErrors = voterVotes.Any(x => !detailsByWorkNo.ContainsKey(x.WorkNo))
            };
            db.ContestVotes.Add(cv);

            foreach (VoteEntry vote in voterVotes)
            {
                if (!detailsByWorkNo.TryGetValue(vote.WorkNo, out RmContestDetail? detail))
                    continue;

                bool selfVote = contest.TreatSelfVoteAsZero && detail.CdPId == voter.PId;
                decimal effectiveRate = selfVote ? 0m : vote.Score;
                db.Votes.Add(new RmVote
                {
                    VId = db.NextVoteId++,
                    VCvId = cv.CvId,
                    VCdId = detail.CdId,
                    VCId = contestId,
                    VPId = voter.PId,
                    VCRNo = vote.WorkNo,
                    VRate = effectiveRate,
                    VPRate = vote.Score,
                    ScoreText = vote.ScoreText,
                    Comment = vote.Comment
                });
            }
        }
    }

    private static void RecalculateTotals(RhymeMachineDb db, int contestId)
    {
        foreach (RmContestDetail detail in db.ContestDetails.Where(x => x.CdCId == contestId))
        {
            detail.CdRate = db.Votes.Where(x => x.VCId == contestId && x.VCdId == detail.CdId).Sum(x => x.VRate);
        }

        foreach (RmContestVote cv in db.ContestVotes.Where(x => x.CvCId == contestId))
        {
            cv.CvRate = db.Votes.Where(x => x.VCId == contestId && x.VCvId == cv.CvId).Sum(x => x.VRate);
        }
    }

    private static void RebuildWinners(RhymeMachineDb db, int contestId)
    {
        db.Winners.RemoveAll(x => x.WCId == contestId);

        var ordered = db.ContestDetails
            .Where(x => x.CdCId == contestId)
            .OrderByDescending(x => x.CdRate)
            .ThenBy(x => x.CdRNo)
            .ToList();

        int index = 0;
        foreach (IGrouping<decimal, RmContestDetail> group in ordered.GroupBy(x => x.CdRate).OrderByDescending(x => x.Key))
        {
            int from = index + 1;
            int to = index + group.Count();
            foreach (RmContestDetail detail in group)
            {
                db.Winners.Add(new RmWinner
                {
                    WCId = contestId,
                    WPlaceNo = from,
                    WPlaceTo = to,
                    WCdId = detail.CdId,
                    WRate = detail.CdRate,
                    WCtId = detail.CdCtId
                });
            }
            index = to;
        }
    }

    private static int TryParseNumber(string? value)
    {
        if (int.TryParse((value ?? string.Empty).Trim(), out int number))
            return number;
        return 0;
    }
}
