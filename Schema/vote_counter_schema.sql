-- VoteCounter local SQLite database.
-- Runtime file: VoteCounter/Database/VoteCounter.db

PRAGMA foreign_keys = ON;

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
