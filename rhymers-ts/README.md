# Rhymers TypeScript Migration

[![Rhymers TS CI](https://github.com/LucasVakhos/votecounter/actions/workflows/rhymers-ts-ci.yml/badge.svg)](https://github.com/LucasVakhos/votecounter/actions/workflows/rhymers-ts-ci.yml)

This folder contains a new TypeScript version of the project, created in parallel with the existing .NET codebase.

## Goals

- Keep the original .NET solution intact.
- Migrate incrementally, module by module.
- Share domain contracts in TypeScript from a single package.

## Structure

- `apps/api`: Node.js + Express API (starting point for `Rhymers.Api` and server parts from `Rhymers.Web`).
- `apps/web`: React + Vite frontend (starting point for Blazor UI migration).
- `packages/shared`: Shared TypeScript domain models and DTOs.

## Quick start

```bash
cd rhymers-ts
npm install
npm run build
```

Run API:

```bash
npm run dev:api
```

Run Web:

```bash
npm run dev:web
```

Run full CI checks locally:

```bash
npm run ci
```
