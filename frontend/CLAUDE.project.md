# CLAUDE.project.md — Frontend (Project-Specific)

## Stack

- **Framework:** React 19.2 (TypeScript, strict mode)
- **Build tool:** Vite
- **Styling:** Tailwind CSS
- **API:** Backend REST API via `VITE_API_BASE_URL`
- **Data grid:** TanStack Table v8.21.3 (`@tanstack/react-table`)

## Routing

| Route | Component | Description |
|---|---|---|
| `/` | — | Redirects to `/workspace` |
| `/invite/:token` | `InvitePage` | Join workspace via invite token |
| `/workspace` | `WorkspacePage` | Main workspace view |

## TanStack Table

Use for all grid and spreadsheet-style views. 
- Define columns with `columnHelper.accessor()`. 
- Use `@tanstack/react-virtual` for row virtualization — never render all rows at once.
- Keep cell editors as separate components; don't inline edit logic inside column defs

## UX Principles

This is a spreadsheet-style data catalog. Design accordingly:

- Optimize for fast, keyboard-friendly editing
- Minimize modal usage — prefer inline editing
- Prefer immediate feedback (optimistic updates where safe)