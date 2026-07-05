# Migration Plan (.NET -> TypeScript)

## Phase 1: Foundation

- Set up TypeScript monorepo.
- Define shared domain contracts in `packages/shared`.
- Add basic API endpoints and web shell.

## Phase 2: Core Domain

- Port models from `src/Rhymers.Core/Models` to `packages/shared`.
- Port services from `src/Rhymers.Core/Services` into `apps/api` domain modules.
- Add persistence adapter (PostgreSQL + Prisma or another ORM).

## Phase 3: API Compatibility

- Recreate endpoints from `src/Rhymers.Api/Controllers`.
- Recreate endpoints from `src/Rhymers.Web/Controllers` where needed.
- Add OpenAPI schema and contract tests.

## Phase 4: UI Migration

- Migrate Blazor pages from `src/Rhymers.Web/Components/Pages` to React routes.
- Keep feature parity for:
  - contests
  - discussions
  - votes
  - sorrow chat
  - direct messages

## Phase 5: Cutover

- Run dual environment in staging.
- Compare key behavior with regression tests.
- Switch production traffic when parity is complete.
