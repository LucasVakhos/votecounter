import { useEffect, useState } from "react";

type HealthResponse = { ok: boolean; service: string };

export function App() {
  const [health, setHealth] = useState<HealthResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetch("http://localhost:4000/health")
      .then(async (res) => {
        if (!res.ok) {
          throw new Error(`HTTP ${res.status}`);
        }
        return (await res.json()) as HealthResponse;
      })
      .then(setHealth)
      .catch((e: unknown) => {
        setError(e instanceof Error ? e.message : "unknown error");
      });
  }, []);

  return (
    <main className="app">
      <section className="card">
        <h1>Rhymers TypeScript</h1>
        <p>Parallel migration workspace is ready.</p>
        {health && <p className="ok">API: {health.service}</p>}
        {error && <p className="error">API check failed: {error}</p>}
      </section>
    </main>
  );
}
