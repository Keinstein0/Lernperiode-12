# UniSK frontend — structure prototype

This is an intentionally plain, static SvelteKit UI prototype. Its information hierarchy borrows from GitHub's repository navigation and lists rather than using large decorative dashboards.

## Pages

- `/`: project index with concise project rows and a small workspace summary.
- `/projects/website-redesign`: project view with conversation, agents grouped by department, and task list. Other sample project links use the same layout.
- `/login`: placeholder for a future sign-in flow.

## Run

```sh
npm ci
npm run dev
```

The prototype uses local sample data. There is no backend integration, authentication, message submission, state mutation or new TypeScript functionality. Actions not yet implemented are visibly labelled as placeholders. Styling is plain CSS in `src/app.css`. No custom logo is included.
