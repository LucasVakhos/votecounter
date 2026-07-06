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

const DEFAULT_API_BASE = "http://localhost:4000";

function formatDate(value: string | undefined): string {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
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
  const [targetFilter, setTargetFilter] = useState<TargetFilter>("all");
  const [authorFilter, setAuthorFilter] = useState("");
  const [reasonFilter, setReasonFilter] = useState("");
  const [moderatorFilter, setModeratorFilter] = useState("");
  const [actionFilter, setActionFilter] = useState<ActionFilter>("all");
  const [fromFilter, setFromFilter] = useState("");
  const [toFilter, setToFilter] = useState("");
  const [comments, setComments] = useState<CommentItem[]>([]);
  const [reviews, setReviews] = useState<ReviewItem[]>([]);
  const [deletedItems, setDeletedItems] = useState<DeletedItem[]>([]);
  const [moderationLog, setModerationLog] = useState<ModerationAction[]>([]);

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
      await fetchJson<{ ok: true }>(`/api/discussions/${targetType === "comment" ? "comments" : "reviews"}/${targetId}/delete`, {
        method: "POST",
        body: JSON.stringify({ reason: deleteReason.trim() || undefined })
      });
      setNotice(`${targetType} deleted`);
      await loadDashboard();
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
      setNotice(`${targetType} restored`);
      await loadDashboard();
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
      setNotice(`${targetType} ${action}d`);
      await loadDashboard();
    } catch (actionError: unknown) {
      setError(actionError instanceof Error ? actionError.message : "unknown error");
    } finally {
      setBusy(false);
    }
  }

  async function createComment(): Promise<void> {
    setBusy(true);
    setError(null);
    setNotice(null);

    try {
      await fetchJson<CommentItem>(`/api/discussions/contests/${encodeURIComponent(contestId)}/comments`, {
        method: "POST",
        headers: {
          "X-User-Name": authorName,
          "X-User-Role": "author"
        },
        body: JSON.stringify({ content: commentDraft.trim() })
      });
      setNotice("comment created");
      await loadDashboard();
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
      await fetchJson<ReviewItem>(
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
      setNotice("review created");
      await loadDashboard();
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
        <label className="reason-field">
          Delete reason
          <input value={deleteReason} onChange={(event) => setDeleteReason(event.target.value)} placeholder="spam, abuse, off-topic" />
        </label>
        <button className="primary" disabled={busy} onClick={() => void loadDashboard()}>
          {busy ? "Refreshing..." : "Refresh dashboard"}
        </button>
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
              </div>
            ))}
          </div>
        </article>
      </section>
    </main>
  );
}
