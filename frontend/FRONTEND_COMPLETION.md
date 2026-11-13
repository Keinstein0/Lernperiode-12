# Frontend completion (local UI features)

This branch extends the backend-integration demo without changing router or node endpoints.

## Conversation

- Renders a safe subset of Markdown (headings, inline code, fenced code blocks, bold, links, quotes, list items) through Svelte's escaped text output. No raw HTML is interpreted.
- Copy individual messages or code blocks, search existing messages, and export conversation text as Markdown.
- Keeps existing task-reference navigation and message composer behaviour.

## Tasks

- Filter by status and agent, search by task description/ID/agent, and sort by creation timestamp.
- Download a completed task report as Markdown.
- For a failed task, **Create retry ticket** submits a *new* ticket with the same parameters. This is not a cancel/retry backend endpoint and requires an available node; duplicate execution is possible if a prior task was not truly stopped.

## Settings

- `/settings` supports a browser-local polling interval (4/10/30 seconds or manual only), compact spacing, and a router connectivity test.
- The connection test uses the current configured API adapter (`VITE_API_MODE=router` in the Docker demo; otherwise mock).
- Polling changes take effect when the relevant project/agent view is reopened.

## Explicitly not implemented by this branch

The following require new backend contracts or a real AI integration: streamed AI tokens, task cancellation, GitHub OAuth and repository browsing, authoritative project/chat persistence, and real agent source-code modifications. The frontend does not claim to provide these features.

## Verification

Svelte component parsing/compilation and TypeScript `tsc --noEmit` were checked locally. A complete SvelteKit `npm run check` / Docker browser test could not run in the current environment because installed Rolldown native bindings are missing.
