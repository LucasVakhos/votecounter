import { useEffect, useState } from "react";

type HealthResponse = {
  ok: boolean;
  service: string;
  storage: string;
  schemaVersion: number;
};

type CommentItem = {
  id: string;
  authorName: string;
  authorRole: string;
  content: string;
  createdAt: string;
  likesCount: number;
  isApproved: boolean;
  isHidden: boolean;
  isDeleted: boolean;
  deletedBy?: string;
};

type ReviewItem = {
  id: string;
  reviewerName: string;
  reviewerRole: string;
  title: string;
  content: string;
  createdAt: string;
  helpfulCount: number;
  rating?: number;
  isApproved: boolean;
  isHidden: boolean;
  isDeleted: boolean;
  deletedBy?: string;
};

type DeletedItem = {
  targetType: "comment" | "review";
  targetId: string;
  contestId: string;
  authorName: string;
  deletedBy: string;
  deletedAt: string;
  reason?: string;
  originalContent: string;
};

type ModerationAction = {
  id: string;
  moderatorName: string;
  action: "delete" | "restore" | "hide" | "approve";
  targetType: "comment" | "review";
  targetId: string;
  reason?: string;
  performedAt: string;
};

type TargetFilter = "all" | "comment" | "review";
type ActionFilter = "all" | "delete" | "restore" | "hide" | "approve";
type TimelineTarget = { targetType: "comment" | "review"; targetId: string } | null;
type ParticipantRole = "reader" | "author" | "moderator" | "admin";

const DEFAULT_API_BASE = "http://localhost:4000";

function formatDate(value: string | undefined): string {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

function getLiveHelp(role: ParticipantRole, context: {
  contestId: string;
  workNumber: string;
  commentsCount: number;
  reviewsCount: number;
  deletedCount: number;
  timelineTarget: TimelineTarget;
  hasFilters: boolean;
}): { headline: string; steps: string[] } {
  if (role === "reader") {
    return {
      headline: "Reader help",
      steps: [
        `Open contest ${context.contestId} to inspect visible comments and reviews without changing state.`,
        context.hasFilters ? "Clear filters if you want the full picture before reporting an issue." : "Use filters to focus on a specific author or moderation reason.",
        context.deletedCount > 0 ? "Deleted items exist, so ask a moderator if you need an explanation for a missing post." : "No deleted items are visible right now, so the discussion is currently clean."
      ]
    };
  }

  if (role === "author") {
    return {
      headline: "Author help",
      steps: [
        `Create a comment or a review for work ${context.workNumber} to test the full publishing flow.`,
        context.commentsCount === 0 ? "Start with a comment so moderators have something concrete to review." : "Comments already exist, so you can iterate on moderation scenarios immediately.",
        context.reviewsCount === 0 ? "Add a review next to verify helpful, hide, and delete actions." : "Reviews are present, so you can test helpful and moderation actions live."
      ]
    };
  }

  if (role === "admin") {
    return {
      headline: "Admin help",
      steps: [
        "Audit moderation log entries first, then drill into a specific timeline when something looks suspicious.",
        context.timelineTarget ? `Timeline is locked to ${context.timelineTarget.targetType}:${context.timelineTarget.targetId}; clear it to resume broad oversight.` : "Select Timeline on any item to inspect a full action chain on one target.",
        context.deletedCount > 0 ? "Restore items only after checking reason and matching the deletion against policy." : "No deleted items are waiting, so focus on approval and hidden-state hygiene."
      ]
    };
  }

  return {
    headline: "Moderator help",
    steps: [
      context.commentsCount + context.reviewsCount === 0
        ? "No active discussion data yet. Create a comment or review first, then test moderation actions."
        : "Use quick actions on cards to moderate without reloading the whole dashboard.",
      context.timelineTarget
        ? `You are tracing ${context.timelineTarget.targetType}:${context.timelineTarget.targetId}. Use this to confirm delete/restore order.`
        : "Pick Timeline on a comment, review, deleted item, or log entry to inspect one target end-to-end.",
      context.hasFilters ? "Filters are active; if counts look odd, clear them before assuming data is missing." : "No filters are active, so current counts reflect the whole selected contest/work scope."
    ]
  };
}

function createOptimisticLogEntry(
  moderatorName: string,
  action: ModerationAction["action"],
  targetType: ModerationAction["targetType"],
  targetId: string,
  reason?: string
): ModerationAction {
  return {
    id: `optimistic-${crypto.randomUUID()}`,
    moderatorName,
    action,
    targetType,
    targetId,
    reason,
    performedAt: new Date().toISOString()
  };
}

export function App() {
  const [health, setHealth] = useState<HealthResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [apiBase, setApiBase] = useState(DEFAULT_API_BASE);
  const [contestId, setContestId] = useState("c1");
  const [workNumber, setWorkNumber] = useState("1");
  const [moderatorName, setModeratorName] = useState("ModUser");
  const [deleteReason, setDeleteReason] = useState("");
  const [authorName, setAuthorName] = useState("Poet");
  const [commentDraft, setCommentDraft] = useState("Fresh comment for moderation flow");
  const [reviewTitle, setReviewTitle] = useState("Strong piece");
  const [reviewDraft, setReviewDraft] = useState("This work has a clear voice and solid rhythm.");
  const [reviewRating, setReviewRating] = useState("8");
  const [participantRole, setParticipantRole] = useState<ParticipantRole>("moderator");
  const [targetFilter, setTargetFilter] = useState<TargetFilter>("all");
  const [authorFilter, setAuthorFilter] = useState("");
  const [reasonFilter, setReasonFilter] = useState("");
  const [moderatorFilter, setModeratorFilter] = useState("");
  const [actionFilter, setActionFilter] = useState<ActionFilter>("all");
  const [fromFilter, setFromFilter] = useState("");
  const [toFilter, setToFilter] = useState("");
  const [timelineTarget, setTimelineTarget] = useState<TimelineTarget>(null);
  const [comments, setComments] = useState<CommentItem[]>([]);
  const [reviews, setReviews] = useState<ReviewItem[]>([]);
  const [deletedItems, setDeletedItems] = useState<DeletedItem[]>([]);
  const [moderationLog, setModerationLog] = useState<ModerationAction[]>([]);

  const liveHelp = getLiveHelp(participantRole, {
    contestId,
    workNumber,
    commentsCount: comments.length,
    reviewsCount: reviews.length,
    deletedCount: deletedItems.length,
    timelineTarget,
    hasFilters: Boolean(targetFilter !== "all" || authorFilter || reasonFilter || moderatorFilter || actionFilter !== "all" || fromFilter || toFilter)
  });

  function pushLogEntry(entry: ModerationAction): void {
    setModerationLog((current) => [entry, ...current].slice(0, 20));
  }

  function updateCommentItem(commentId: string, updater: (comment: CommentItem) => CommentItem): CommentItem | null {
    let updatedComment: CommentItem | null = null;
    setComments((current) =>
      current.map((comment) => {
        if (comment.id !== commentId) {
          return comment;
        }

        updatedComment = updater(comment);
        return updatedComment;
      })
    );
    return updatedComment;
  }

  function updateReviewItem(reviewId: string, updater: (review: ReviewItem) => ReviewItem): ReviewItem | null {
    let updatedReview: ReviewItem | null = null;
    setReviews((current) =>
      current.map((review) => {
        if (review.id !== reviewId) {
          return review;
        }

        updatedReview = updater(review);
        return updatedReview;
      })
    );
    return updatedReview;
  }

  async function fetchJson<T>(path: string, init?: RequestInit): Promise<T> {
    const response = await fetch(`${apiBase}${path}`, {
      ...init,
      headers: {
        "Content-Type": "application/json",
        "X-User-Name": moderatorName,
        "X-User-Role": "moderator",
        ...(init?.headers ?? {})
      }
    });

    if (!response.ok) {
      let message = `HTTP ${response.status}`;
      try {
        const body = (await response.json()) as { error?: string };
        if (body.error) {
          message = body.error;
        }
      } catch {
        // ignore non-json responses
      }
      throw new Error(message);
    }

    if (response.status === 204) {
      return undefined as T;
    }

    return (await response.json()) as T;
  }

  async function loadDashboard(): Promise<void> {
    setBusy(true);
    setError(null);

    try {
      const deletedParams = new URLSearchParams();
      deletedParams.set("contestId", contestId);
      if (targetFilter !== "all") {
        deletedParams.set("targetType", targetFilter);
      }
      if (authorFilter.trim()) {
        deletedParams.set("authorName", authorFilter.trim());
      }
      if (reasonFilter.trim()) {
        deletedParams.set("reason", reasonFilter.trim());
      }
      if (fromFilter) {
        deletedParams.set("from", new Date(fromFilter).toISOString());
      }
      if (toFilter) {
        deletedParams.set("to", new Date(toFilter).toISOString());
      }

      const logParams = new URLSearchParams();
      logParams.set("limit", "20");
      if (targetFilter !== "all") {
        logParams.set("targetType", targetFilter);
      }
      if (moderatorFilter.trim()) {
        logParams.set("moderatorName", moderatorFilter.trim());
      }
      if (actionFilter !== "all") {
        logParams.set("action", actionFilter);
      }
      if (timelineTarget) {
        logParams.set("targetId", timelineTarget.targetId);
      }
      if (reasonFilter.trim()) {
        logParams.set("reason", reasonFilter.trim());
      }
      if (fromFilter) {
        logParams.set("from", new Date(fromFilter).toISOString());
      }
      if (toFilter) {
        logParams.set("to", new Date(toFilter).toISOString());
      }

      const [healthResponse, commentsResponse, reviewsResponse, deletedResponse, logResponse] = await Promise.all([
        fetchJson<HealthResponse>("/health", { headers: {} }),
        fetchJson<CommentItem[]>(`/api/discussions/contests/${encodeURIComponent(contestId)}/comments`),
        fetchJson<ReviewItem[]>(`/api/discussions/contests/${encodeURIComponent(contestId)}/works/${encodeURIComponent(workNumber)}/reviews`),
        fetchJson<DeletedItem[]>(`/api/discussions/moderation/deleted?${deletedParams.toString()}`),
        fetchJson<ModerationAction[]>(`/api/discussions/moderation/log?${logParams.toString()}`)
      ]);

      setHealth(healthResponse);
      setComments(commentsResponse);
      setReviews(reviewsResponse);
      setDeletedItems(deletedResponse);
      setModerationLog(logResponse);
    } catch (loadError: unknown) {
      setError(loadError instanceof Error ? loadError.message : "unknown error");
    } finally {
      setBusy(false);
    }
  }

  useEffect(() => {
    void loadDashboard();
  }, []);

  async function deleteTarget(targetType: "comment" | "review", targetId: string): Promise<void> {
    setBusy(true);
    setError(null);
    setNotice(null);

    try {
      const trimmedReason = deleteReason.trim() || undefined;
      const existingComment = comments.find((comment) => comment.id === targetId);
      const existingReview = reviews.find((review) => review.id === targetId);
      await fetchJson<{ ok: true }>(`/api/discussions/${targetType === "comment" ? "comments" : "reviews"}/${targetId}/delete`, {
        method: "POST",
        body: JSON.stringify({ reason: trimmedReason })
      });

      const deletedAt = new Date().toISOString();
      if (targetType === "comment") {
        const updated = updateCommentItem(targetId, (comment) => ({
          ...comment,
          content: "[Deleted by moderation]",
          isApproved: false,
          isHidden: true,
          isDeleted: true,
          deletedBy: moderatorName
        }));

        if (updated) {
          setDeletedItems((current) => [
            {
              targetType: "comment",
              targetId,
              contestId,
              authorName: updated.authorName,
              deletedBy: moderatorName,
              deletedAt,
              reason: trimmedReason,
              originalContent: existingComment?.content ?? updated.content
            },
            ...current.filter((item) => item.targetId !== targetId)
          ]);
        }
      } else {
        const updated = updateReviewItem(targetId, (review) => ({
          ...review,
          title: "[Deleted by moderation]",
          content: "[Deleted by moderation]",
          isApproved: false,
          isHidden: true,
          isDeleted: true,
          deletedBy: moderatorName
        }));

        if (updated) {
          setDeletedItems((current) => [
            {
              targetType: "review",
              targetId,
              contestId,
              authorName: updated.reviewerName,
              deletedBy: moderatorName,
              deletedAt,
              reason: trimmedReason,
              originalContent: existingReview?.title ?? updated.title
            },
            ...current.filter((item) => item.targetId !== targetId)
          ]);
        }
      }

      pushLogEntry(createOptimisticLogEntry(moderatorName, "delete", targetType, targetId, trimmedReason));
      setNotice(`${targetType} deleted`);
    } catch (actionError: unknown) {
      setError(actionError instanceof Error ? actionError.message : "unknown error");
    } finally {
      setBusy(false);
    }
  }

  async function restoreTarget(targetType: "comment" | "review", targetId: string): Promise<void> {
    setBusy(true);
    setError(null);
    setNotice(null);

    try {
      await fetchJson<{ ok: true }>(`/api/discussions/${targetType === "comment" ? "comments" : "reviews"}/${targetId}/restore`, {
        method: "POST"
      });

      if (targetType === "comment") {
        updateCommentItem(targetId, (comment) => ({
          ...comment,
          content: deletedItems.find((item) => item.targetId === targetId)?.originalContent ?? comment.content,
          isApproved: true,
          isHidden: false,
          isDeleted: false,
          deletedBy: undefined
        }));
      } else {
        updateReviewItem(targetId, (review) => ({
          ...review,
          title: deletedItems.find((item) => item.targetId === targetId)?.originalContent ?? review.title,
          content: review.content === "[Deleted by moderation]" ? review.content : review.content,
          isApproved: true,
          isHidden: false,
          isDeleted: false,
          deletedBy: undefined
        }));
      }

      setDeletedItems((current) => current.filter((item) => item.targetId !== targetId));
      pushLogEntry(createOptimisticLogEntry(moderatorName, "restore", targetType, targetId));
      setNotice(`${targetType} restored`);
    } catch (actionError: unknown) {
      setError(actionError instanceof Error ? actionError.message : "unknown error");
    } finally {
      setBusy(false);
    }
  }

  async function runTargetAction(targetType: "comment" | "review", targetId: string, action: string): Promise<void> {
    setBusy(true);
    setError(null);
    setNotice(null);

    try {
      await fetchJson<{ ok: true }>(`/api/discussions/${targetType === "comment" ? "comments" : "reviews"}/${targetId}/${action}`, {
        method: "POST"
      });

      if (targetType === "comment") {
        updateCommentItem(targetId, (comment) => {
          if (action === "like") {
            return { ...comment, likesCount: comment.likesCount + 1 };
          }
          if (action === "approve") {
            return { ...comment, isApproved: true, isHidden: false };
          }
          if (action === "hide") {
            return { ...comment, isHidden: true };
          }
          return comment;
        });
      } else {
        updateReviewItem(targetId, (review) => {
          if (action === "helpful") {
            return { ...review, helpfulCount: review.helpfulCount + 1 };
          }
          if (action === "approve") {
            return { ...review, isApproved: true, isHidden: false };
          }
          if (action === "hide") {
            return { ...review, isHidden: true };
          }
          return review;
        });
      }

      if (action === "approve" || action === "hide") {
        pushLogEntry(createOptimisticLogEntry(moderatorName, action, targetType, targetId));
      }

      setNotice(`${targetType} ${action}d`);
    } catch (actionError: unknown) {
      setError(actionError instanceof Error ? actionError.message : "unknown error");
    } finally {
      setBusy(false);
    }
  }

  function openTimeline(targetType: "comment" | "review", targetId: string): void {
    setTimelineTarget({ targetType, targetId });
  }

  async function createComment(): Promise<void> {
    setBusy(true);
    setError(null);
    setNotice(null);

    try {
      const created = await fetchJson<CommentItem>(`/api/discussions/contests/${encodeURIComponent(contestId)}/comments`, {
        method: "POST",
        headers: {
          "X-User-Name": authorName,
          "X-User-Role": "author"
        },
        body: JSON.stringify({ content: commentDraft.trim() })
      });
      setComments((current) => [created, ...current]);
      setNotice("comment created");
      setCommentDraft("");
    } catch (actionError: unknown) {
      setError(actionError instanceof Error ? actionError.message : "unknown error");
    } finally {
      setBusy(false);
    }
  }

  async function createReview(): Promise<void> {
    setBusy(true);
    setError(null);
    setNotice(null);

    try {
      const parsedRating = Number.parseInt(reviewRating, 10);
      const created = await fetchJson<ReviewItem>(
        `/api/discussions/contests/${encodeURIComponent(contestId)}/works/${encodeURIComponent(workNumber)}/reviews`,
        {
          method: "POST",
          headers: {
            "X-User-Name": authorName,
            "X-User-Role": "author"
          },
          body: JSON.stringify({
            title: reviewTitle.trim(),
            content: reviewDraft.trim(),
            rating: Number.isNaN(parsedRating) ? undefined : parsedRating
          })
        }
      );
      setReviews((current) => [created, ...current]);
      setNotice("review created");
      setReviewTitle("");
      setReviewDraft("");
    } catch (actionError: unknown) {
      setError(actionError instanceof Error ? actionError.message : "unknown error");
    } finally {
      setBusy(false);
    }
  }

  return (
    <main className="app">
      <section className="hero card">
        <div>
          <p className="eyebrow">Rhymers TS moderation</p>
          <h1>Moderator control room</h1>
          <p className="lead">Delete, restore, and review moderation history without leaving the web app.</p>
        </div>
        <div className="health-panel">
          <p><strong>API</strong> {health?.service ?? "loading..."}</p>
          <p><strong>Storage</strong> {health?.storage ?? "-"}</p>
          <p><strong>Schema</strong> v{health?.schemaVersion ?? "-"}</p>
        </div>
      </section>

      <section className="card controls">
        <label>
          API base
          <input value={apiBase} onChange={(event) => setApiBase(event.target.value)} />
        </label>
        <label>
          Contest ID
          <input value={contestId} onChange={(event) => setContestId(event.target.value)} />
        </label>
        <label>
          Work number
          <input value={workNumber} onChange={(event) => setWorkNumber(event.target.value)} />
        </label>
        <label>
          Moderator name
          <input value={moderatorName} onChange={(event) => setModeratorName(event.target.value)} />
        </label>
        <label>
          Author name
          <input value={authorName} onChange={(event) => setAuthorName(event.target.value)} />
        </label>
        <label>
          Help role
          <select value={participantRole} onChange={(event) => setParticipantRole(event.target.value as ParticipantRole)}>
            <option value="reader">Reader</option>
            <option value="author">Author</option>
            <option value="moderator">Moderator</option>
            <option value="admin">Admin</option>
          </select>
        </label>
        <label className="reason-field">
          Delete reason
          <input value={deleteReason} onChange={(event) => setDeleteReason(event.target.value)} placeholder="spam, abuse, off-topic" />
        </label>
        <button className="primary" disabled={busy} onClick={() => void loadDashboard()}>
          {busy ? "Refreshing..." : "Refresh dashboard"}
        </button>
      </section>

      <section className="card panel">
        <div className="panel-header">
          <h2>Live help</h2>
          <span>{participantRole}</span>
        </div>
        <div className="stack">
          <p><strong>{liveHelp.headline}</strong></p>
          {liveHelp.steps.map((step) => (
            <div className="item" key={step}>
              <p>{step}</p>
            </div>
          ))}
        </div>
      </section>

      <section className="card controls filter-controls">
        <label>
          Target type
          <select value={targetFilter} onChange={(event) => setTargetFilter(event.target.value as TargetFilter)}>
            <option value="all">All</option>
            <option value="comment">Comments</option>
            <option value="review">Reviews</option>
          </select>
        </label>
        <label>
          Author filter
          <input value={authorFilter} onChange={(event) => setAuthorFilter(event.target.value)} placeholder="Poet, Critic..." />
        </label>
        <label>
          Moderator filter
          <input value={moderatorFilter} onChange={(event) => setModeratorFilter(event.target.value)} placeholder="ModUser..." />
        </label>
        <label>
          Action filter
          <select value={actionFilter} onChange={(event) => setActionFilter(event.target.value as ActionFilter)}>
            <option value="all">All</option>
            <option value="delete">Delete</option>
            <option value="restore">Restore</option>
            <option value="hide">Hide</option>
            <option value="approve">Approve</option>
          </select>
        </label>
        <label>
          Reason filter
          <input value={reasonFilter} onChange={(event) => setReasonFilter(event.target.value)} placeholder="abuse, off-topic..." />
        </label>
        <label>
          From
          <input type="datetime-local" value={fromFilter} onChange={(event) => setFromFilter(event.target.value)} />
        </label>
        <label>
          To
          <input type="datetime-local" value={toFilter} onChange={(event) => setToFilter(event.target.value)} />
        </label>
        <button className="primary" disabled={busy} onClick={() => void loadDashboard()}>
          Apply filters
        </button>
        {timelineTarget && (
          <button
            className="secondary"
            disabled={busy}
            onClick={() => {
              setTimelineTarget(null);
              void loadDashboard();
            }}
          >
            Clear timeline
          </button>
        )}
      </section>

      <section className="grid composer-grid">
        <article className="card panel">
          <div className="panel-header">
            <h2>Create comment</h2>
          </div>
          <div className="composer-fields">
            <label>
              Comment text
              <textarea value={commentDraft} onChange={(event) => setCommentDraft(event.target.value)} rows={5} />
            </label>
            <button
              className="primary"
              disabled={busy || !commentDraft.trim() || !authorName.trim()}
              onClick={() => void createComment()}
            >
              Create comment
            </button>
          </div>
        </article>

        <article className="card panel">
          <div className="panel-header">
            <h2>Create review</h2>
          </div>
          <div className="composer-fields">
            <label>
              Review title
              <input value={reviewTitle} onChange={(event) => setReviewTitle(event.target.value)} />
            </label>
            <label>
              Review text
              <textarea value={reviewDraft} onChange={(event) => setReviewDraft(event.target.value)} rows={5} />
            </label>
            <label>
              Rating
              <input value={reviewRating} onChange={(event) => setReviewRating(event.target.value)} />
            </label>
            <button
              className="primary"
              disabled={busy || !reviewTitle.trim() || !reviewDraft.trim() || !authorName.trim()}
              onClick={() => void createReview()}
            >
              Create review
            </button>
          </div>
        </article>
      </section>

      {notice && (
        <section className="card notice">
          <p>{notice}</p>
        </section>
      )}

      {error && (
        <section className="card error-banner">
          <p>Request failed: {error}</p>
        </section>
      )}

      <section className="grid">
        <article className="card panel">
          <div className="panel-header">
            <h2>Comments</h2>
            <span>{comments.length}</span>
          </div>
          <div className="stack">
            {comments.length === 0 && <p className="muted">No comments for this contest.</p>}
            {comments.map((comment) => (
              <div className="item" key={comment.id}>
                <div className="item-meta">
                  <strong>{comment.authorName}</strong>
                  <span>{comment.authorRole}</span>
                  <span>{formatDate(comment.createdAt)}</span>
                </div>
                <p>{comment.content}</p>
                <div className="item-footer">
                  <span>Likes: {comment.likesCount}</span>
                  <span>{comment.isApproved ? "Approved" : "Pending"}</span>
                  <span>{comment.isHidden ? "Hidden" : "Visible"}</span>
                  <span>{comment.isDeleted ? `Deleted by ${comment.deletedBy ?? "?"}` : "Active"}</span>
                </div>
                <div className="actions">
                  <button disabled={busy} onClick={() => openTimeline("comment", comment.id)}>
                    Timeline
                  </button>
                  {!comment.isDeleted && (
                    <button disabled={busy} onClick={() => void runTargetAction("comment", comment.id, "like")}>
                      Like
                    </button>
                  )}
                  {!comment.isDeleted && (
                    <button disabled={busy} onClick={() => void runTargetAction("comment", comment.id, "approve")}>
                      Approve
                    </button>
                  )}
                  {!comment.isDeleted && (
                    <button disabled={busy} onClick={() => void runTargetAction("comment", comment.id, "hide")}>
                      Hide
                    </button>
                  )}
                  {!comment.isDeleted && (
                    <button disabled={busy} onClick={() => void deleteTarget("comment", comment.id)}>
                      Delete
                    </button>
                  )}
                  {comment.isDeleted && (
                    <button disabled={busy} onClick={() => void restoreTarget("comment", comment.id)}>
                      Restore
                    </button>
                  )}
                </div>
              </div>
            ))}
          </div>
        </article>

        <article className="card panel">
          <div className="panel-header">
            <h2>Reviews</h2>
            <span>{reviews.length}</span>
          </div>
          <div className="stack">
            {reviews.length === 0 && <p className="muted">No reviews for this work.</p>}
            {reviews.map((review) => (
              <div className="item" key={review.id}>
                <div className="item-meta">
                  <strong>{review.reviewerName}</strong>
                  <span>{review.reviewerRole}</span>
                  <span>{formatDate(review.createdAt)}</span>
                </div>
                <p><strong>{review.title}</strong></p>
                <p>{review.content}</p>
                <div className="item-footer">
                  <span>Helpful: {review.helpfulCount}</span>
                  <span>Rating: {review.rating ?? "-"}</span>
                  <span>{review.isApproved ? "Approved" : "Pending"}</span>
                  <span>{review.isHidden ? "Hidden" : "Visible"}</span>
                  <span>{review.isDeleted ? `Deleted by ${review.deletedBy ?? "?"}` : "Active"}</span>
                </div>
                <div className="actions">
                  <button disabled={busy} onClick={() => openTimeline("review", review.id)}>
                    Timeline
                  </button>
                  {!review.isDeleted && (
                    <button disabled={busy} onClick={() => void runTargetAction("review", review.id, "helpful")}>
                      Helpful
                    </button>
                  )}
                  {!review.isDeleted && (
                    <button disabled={busy} onClick={() => void runTargetAction("review", review.id, "approve")}>
                      Approve
                    </button>
                  )}
                  {!review.isDeleted && (
                    <button disabled={busy} onClick={() => void runTargetAction("review", review.id, "hide")}>
                      Hide
                    </button>
                  )}
                  {!review.isDeleted && (
                    <button disabled={busy} onClick={() => void deleteTarget("review", review.id)}>
                      Delete
                    </button>
                  )}
                  {review.isDeleted && (
                    <button disabled={busy} onClick={() => void restoreTarget("review", review.id)}>
                      Restore
                    </button>
                  )}
                </div>
              </div>
            ))}
          </div>
        </article>
      </section>

      <section className="grid">
        <article className="card panel">
          <div className="panel-header">
            <h2>Target timeline</h2>
            <span>{moderationLog.length}</span>
          </div>
          <div className="stack">
            <p className="muted">
              {timelineTarget ? `${timelineTarget.targetType}:${timelineTarget.targetId}` : "Select Timeline on a comment, review, deleted item, or log entry."}
            </p>
            {moderationLog.length === 0 && <p className="muted">No moderation actions for current timeline/filter state.</p>}
            {moderationLog.map((entry) => (
              <div className="item" key={`timeline-${entry.id}`}>
                <div className="item-meta">
                  <strong>{entry.action}</strong>
                  <span>{entry.targetType}</span>
                  <span>{formatDate(entry.performedAt)}</span>
                </div>
                <p>
                  {entry.moderatorName} acted on {entry.targetId}
                </p>
                <div className="item-footer">
                  <span>Reason: {entry.reason ?? "-"}</span>
                </div>
              </div>
            ))}
          </div>
        </article>

        <article className="card panel">
          <div className="panel-header">
            <h2>Deleted items</h2>
            <span>{deletedItems.length}</span>
          </div>
          <div className="stack">
            {deletedItems.length === 0 && <p className="muted">No deleted items for this contest.</p>}
            {deletedItems.map((item) => (
              <div className="item" key={`${item.targetType}-${item.targetId}`}>
                <div className="item-meta">
                  <strong>{item.targetType}</strong>
                  <span>{item.authorName}</span>
                  <span>{formatDate(item.deletedAt)}</span>
                </div>
                <p>{item.originalContent}</p>
                <div className="item-footer">
                  <span>Reason: {item.reason ?? "-"}</span>
                  <span>Deleted by: {item.deletedBy}</span>
                </div>
                <div className="actions">
                  <button disabled={busy} onClick={() => openTimeline(item.targetType, item.targetId)}>
                    Timeline
                  </button>
                  <button disabled={busy} onClick={() => void restoreTarget(item.targetType, item.targetId)}>
                    Restore
                  </button>
                </div>
              </div>
            ))}
          </div>
        </article>

        <article className="card panel">
          <div className="panel-header">
            <h2>Moderation log</h2>
            <span>{moderationLog.length}</span>
          </div>
          <div className="stack">
            {moderationLog.length === 0 && <p className="muted">No moderation actions yet.</p>}
            {moderationLog.map((entry) => (
              <div className="item" key={entry.id}>
                <div className="item-meta">
                  <strong>{entry.action}</strong>
                  <span>{entry.targetType}</span>
                  <span>{formatDate(entry.performedAt)}</span>
                </div>
                <p>
                  {entry.moderatorName} acted on {entry.targetId}
                </p>
                <div className="item-footer">
                  <span>Reason: {entry.reason ?? "-"}</span>
                </div>
                <div className="actions">
                  <button disabled={busy} onClick={() => openTimeline(entry.targetType, entry.targetId)}>
                    Timeline
                  </button>
                </div>
              </div>
            ))}
          </div>
        </article>
      </section>
    </main>
  );
}
