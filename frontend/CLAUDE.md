# CLAUDE.md — Frontend

> Architecture rules are defined in the root `CLAUDE.md`. This file covers frontend-specific implementation details.

## Folder Structure

```
frontend/
  components/   # UI rendering only
  hooks/        # Component logic and data fetching
  services/     # API call functions
  utils/        # Pure helper functions
```

New pages go in `components/pages/` and must be registered in `App.tsx`.

## Separation of Concerns

| Layer        | Responsibility                            |
|--------------|-------------------------------------------|
| Component    | Render UI, respond to user events         |
| Hook         | Encapsulate component logic, call services |
| Service      | Raw API calls — no UI logic               |
| Util         | Pure, reusable helper functions           |

Components must not contain raw `fetch` calls or business logic. Push that into hooks and services.

<!-- Code example included because the rule is easy to misjudge without seeing a concrete violation -->

```tsx
// ✅
const { tables, isLoading } = useTables();

// ❌
const [tables, setTables] = useState([]);
useEffect(() => { fetch('/api/tables').then(...) }, []);
```

## Component Rules

- Functional components only (no class components)
- Small, single-purpose — split when a component does too much
- Hierarchy: Page → Feature components → Reusable UI components
- Always handle loading and error states
- Pages that require authentication or a session must render a user-facing message when the required context is absent — never return `null` and never redirect to `/`

## State Management

Default to local state. Lift only when siblings genuinely share state.

```tsx
// Prefer
const [value, setValue] = useState('');

// Use useMemo for expensive derived values
const sorted = useMemo(() => [...items].sort(...), [items]);
```

Avoid unnecessary global state.

## TypeScript

- Strict mode is enabled — no `any`
- Define explicit types for all API responses and component props
- Use `type` for shapes, `interface` for extendable contracts

## Custom Hooks

Name hooks after the resource or behavior they manage.

## Performance

Apply `useMemo`, `useCallback`, and `React.memo` only where there is a measurable re-render problem. Do not add them preemptively.

## Styling

- Use Tailwind utility classes consistently
- No inline styles unless dynamically computed
- Follow existing patterns in the codebase before introducing new ones

## Local Configuration

- Environment variables go in `.env` or `.env.local` (both gitignored, never committed)
- Never hardcode API URLs or secrets in source files — use `import.meta.env.VITE_*` variables

## Testing

**Unit tests** cover hooks, services, and utils.
- Use Vitest
- Mock API calls in service tests — never hit the real backend
- Test hooks with `renderHook` from React Testing Library

**Component tests** cover user interactions.
- Use React Testing Library — query by role and label, not by class or id
- Test behavior, not implementation details
- Do not test pure styling or Tailwind classes

**What not to test:**
- Third-party library behavior
- Implementation details like internal state variable names
