namespace VoteCounter.Core.Models;

/// <summary>
/// Локальное зеркало базы Rhyme Machine.
/// Основное хранение теперь в SQLite-файле Database/VoteCounter.db в папке проекта.
/// Названия сущностей сохранены близко к таблицам старой базы, чтобы дальше
/// можно было без боли перейти на Firebird/SQL Server.
/// </summary>
public sealed class RhymeMachineDb
{
    public int SchemaVersion { get; set; } = 4;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public int NextPoetId { get; set; } = 1;
    public int NextContestNameId { get; set; } = 1;
    public int NextContestId { get; set; } = 1;
    public int NextContestNameMemberId { get; set; } = 1;
    public int NextContestDetailId { get; set; } = 1;
    public int NextContestVoteId { get; set; } = 1;
    public int NextVoteId { get; set; } = 1;
    public int NextRhymeId { get; set; } = 1;
    public int NextVoteRuleId { get; set; } = 1;
    public int NextVoteValueId { get; set; } = 1;

    public List<RmPoet> Poets { get; set; } = new();
    public List<RmContestName> ContestNames { get; set; } = new();
    public List<RmContest> Contests { get; set; } = new();
    public List<RmContestNameMember> ContestNameMembers { get; set; } = new();
    public List<RmContestDetail> ContestDetails { get; set; } = new();
    public List<RmContestVote> ContestVotes { get; set; } = new();
    public List<RmVote> Votes { get; set; } = new();
    public List<RmRhyme> Rhymes { get; set; } = new();
    public List<RmVoteRule> VoteRules { get; set; } = new();
    public List<RmVoteValue> VoteValues { get; set; } = new();
    public List<RmWinner> Winners { get; set; } = new();
}

/// <summary>POETS.</summary>
public sealed class RmPoet
{
    public int PId { get; set; }
    public string PNameId { get; set; } = string.Empty;
    public string PName { get; set; } = string.Empty;
    public string PNickname { get; set; } = string.Empty;
    public string PUrl { get; set; } = string.Empty;
    public bool PNotify { get; set; } = true;
    public bool PIsPoet { get; set; } = true;
}

/// <summary>CONCURSES_NAMES + основные правила голосования.</summary>
public sealed class RmContestName
{
    public int CnId { get; set; }
    public string SourceContestId { get; set; } = string.Empty;
    public string CnName { get; set; } = string.Empty;
    public int CnNumGen { get; set; }
    public int CnBaseVote { get; set; } = 3;
    public int CnMaxVote { get; set; } = 4;
    public bool CnOneOnTopic { get; set; }
    public int CnLimitMaxVote { get; set; }
    public bool CnLimitMaxVoteByTopic { get; set; }
    public bool CnMoreToOne { get; set; } = true;
    public bool CnPenaltyNoVote { get; set; } = true;
    public bool CnRemoveNoVote { get; set; } = true;
    public bool CnCanNullRate { get; set; }
    public bool CnCanVoteForSelf { get; set; }
    public int? CnCId { get; set; }
}

/// <summary>CN_VOTE_RULS.</summary>
public sealed class RmVoteRule
{
    public int CnvrId { get; set; }
    public int CnwrCnId { get; set; }
    public double CnwrVotePercent { get; set; } = 33;
    public int CnwrBaseVote { get; set; } = 3;
    public int CnwrMaxVote { get; set; } = 4;
    public bool CnwrRulerOutOfContest { get; set; } = true;
    public bool CnwrOneOnTopic { get; set; }
    public int CnwrLimitMaxVote { get; set; }
    public bool CnwrLimitMaxVoteByTopic { get; set; }
    public bool CnwrMoreToOne { get; set; } = true;
    public bool CnwrPenaltyNoVote { get; set; } = true;
    public bool CnwrRemoveNoVote { get; set; } = true;
    public bool CnwrCanNullRate { get; set; }
    public bool CnwrCanVoteForSelf { get; set; }
}

/// <summary>CN_VOTE_VALUES.</summary>
public sealed class RmVoteValue
{
    public int CnvvId { get; set; }
    public int CnvvCnvrId { get; set; }
    public int CnvvValue { get; set; }
    public int CnvvQty { get; set; }
    public bool CnvvNeedNote { get; set; }
}

/// <summary>CONCURSES.</summary>
public sealed class RmContest
{
    public int CId { get; set; }
    public string SourceContestId { get; set; } = string.Empty;
    public int CCnId { get; set; }
    public int CNum { get; set; }
    public DateTime CDate { get; set; } = DateTime.Today;
    public DateTime? CDateVote { get; set; }
    public DateTime? CDateEnd { get; set; }
    public int CTopics { get; set; }
    public int CDtl { get; set; }
    public int CPoets { get; set; }
    public int CVoteMax { get; set; }
    public int CVotes { get; set; }
    public bool COpened { get; set; } = true;
    public bool CReceptClosed { get; set; } = true;
    public bool CVoteClosed { get; set; }
    public bool CClosed { get; set; }
    public int CStadia { get; set; } = 2;
}

/// <summary>CONCURSES_NAMES_MEMBERS.</summary>
public sealed class RmContestNameMember
{
    public int CnmId { get; set; }
    public int CnmPId { get; set; }
    public int CnmCnId { get; set; }
    public bool CnmIsPoet { get; set; }
    public int CnmCd { get; set; }
    public bool CnmActive { get; set; } = true;
}

/// <summary>RHYMES.</summary>
public sealed class RmRhyme
{
    public int RId { get; set; }
    public int RPId { get; set; }
    public string RContent { get; set; } = string.Empty;
    public string RUrl { get; set; } = string.Empty;
}

/// <summary>CONCURS_DTL.</summary>
public sealed class RmContestDetail
{
    public int CdId { get; set; }
    public int CdCId { get; set; }
    public int CdRNo { get; set; }
    public bool CdOutOfContest { get; set; }
    public bool CdClosed { get; set; }
    public int CdPId { get; set; }
    public int? CdCtId { get; set; }
    public string Topic { get; set; } = string.Empty;
    public int CdRId { get; set; }
    public decimal CdRate { get; set; }
    public string Title { get; set; } = string.Empty;
}

/// <summary>CONCURS_VOTES: один пакет голосования от одного голосующего в конкурсе.</summary>
public sealed class RmContestVote
{
    public int CvId { get; set; }
    public int CvCId { get; set; }
    public int CvPId { get; set; }
    public DateTime CvTimestamp { get; set; } = DateTime.Now;
    public string CvOriginal { get; set; } = string.Empty;
    public bool CvVoted { get; set; }
    public bool CvErrors { get; set; }
    public decimal CvRate { get; set; }
}

/// <summary>VOTES: одна оценка за одну работу.</summary>
public sealed class RmVote
{
    public int VId { get; set; }
    public int VCvId { get; set; }
    public int VCdId { get; set; }
    public int VCId { get; set; }
    public int VPId { get; set; }
    public int VCRNo { get; set; }
    public decimal VRate { get; set; }
    public decimal VPRate { get; set; }
    public int? VVnId { get; set; }
    public string ScoreText { get; set; } = string.Empty;
    public string RuleNote { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
}

/// <summary>WINNERS.</summary>
public sealed class RmWinner
{
    public int WCId { get; set; }
    public int WPlaceNo { get; set; }
    public int WPlaceTo { get; set; }
    public int WCdId { get; set; }
    public decimal WRate { get; set; }
    public int? WCtId { get; set; }
}
