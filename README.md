CivicConnect is a web app for reporting and tracking facility problems at Riverside Community Centre. It replaces reports sent by email, phone, WhatsApp and paper with one place where every request can be followed until it is fixed.

Built for the **SEN381 Software Engineering** project with ASP.NET Core MVC (Razor), cookie sign-in and MongoDB.

## Features

- Report a problem with a type, a room on the floor plan and a priority
- Three roles: requesters report, staff fix the jobs assigned to them, a supervisor assigns and closes
- Requests move one step at a time: New, Assigned, In Progress, Resolved, Closed, with a full history
- In-app updates (the bell) when a job is assigned, started, fixed or closed
- Facility map showing which rooms have open requests
- Search and filter by status, and a People page for managing accounts

## Running locally

All data is stored in MongoDB Atlas, in the `civicconnect` database (`users`, `categories`, `requests`). Keep the connection string in user secrets so it never ends up in the repository:

```powershell
dotnet user-secrets set "ConnectionStrings:MongoDb" "<Atlas connection string>" --project src/CivicConnect.Web
dotnet run --project src/CivicConnect.Web
```

The site opens at http://localhost:5200. The problem types and database indexes are created on startup.

## Demo on Render

The app is a Docker web service (`Dockerfile`, `render.yaml`). Render injects `PORT`; the image listens on it. Set these in the Render dashboard, not in git:

- `ConnectionStrings__MongoDb` — the Atlas connection string
- `ASPNETCORE_ENVIRONMENT` — `Production`
- `APP_VERSION` — the release tag, so `/health` and the footer match the demo

In Atlas, allow Render to connect (Network Access `0.0.0.0/0` on the free cluster). The free Render service sleeps when idle, so open the URL once before recording. Accounts already in the `civicconnect` database are the ones the demo signs in with.

## Accounts

Passwords are stored as hashes, so accounts cannot be typed into Atlas directly. Create them from a file:

1. Copy `accounts.example.json` to `accounts.json` (ignored by git) and fill in each person's name, email, role (`Supervisor`, `Staff` or `Requester`) and a password of at least 8 characters.
2. Run the import from the repository root:

   ```powershell
   dotnet run --project src/CivicConnect.Web -- import-accounts "$PWD\accounts.json"
   ```

It prints `Added` or `Skipped` for each email and imports nothing if any entry is wrong. After that, a supervisor can add people and set new passwords on the **People** page, and anyone can change their own password by clicking their name.

## Tests

```powershell
dotnet test
```

- `RequestServiceTests` and `StatusTransitionPolicyTests` are unit tests of the business rules with in-memory fakes.
- `BlackBox/` is a decision table of who may assign, start, fix or close a request, plus boundary-value tests of the 8-character password rule.
- `Integration/` runs the whole app (controllers, sign-in, views and the MongoDB stores) against a throwaway MongoDB 8 server started by [EphemeralMongo](https://github.com/asimmon/ephemeral-mongo). Each test gets its own empty database, so nothing touches Atlas. The first run downloads MongoDB, which takes a minute or two.
- `EndToEnd/` drives Chromium (Playwright) through the report → assign → fix → close journey. Install the browser once after a build: `powershell -File tests/CivicConnect.Web.Tests/bin/Release/net10.0/playwright.ps1 install chromium`
- `Performance/` loads 500 requests and times the supervisor "All requests" search for 10 concurrent users. The numbers are student-scale; they do not represent a municipal production load.
- Tests are tagged with the requirement they check, e.g. `dotnet test --filter Requirement=FR-07` or `--filter Category=E2E`.

`GET /health` returns `{ status, version, environment }` and is used to confirm which build is running. The same version string is in the page footer. Point a staging run at a separate Atlas database and set `APP_VERSION` (or rely on `GITHUB_SHA`) so the footer matches the tagged commit.

## CI

Every pull request runs:

- **Build with static analysis:** .NET analyzers at `latest-recommended`; any warning fails the build (`Directory.Build.props`).
- **Tests and coverage:** all tests, with a coverage summary on the run page and the results uploaded as the `test-results` artifact.
- **Dependencies:** fails if any NuGet package has a known vulnerability. Dependabot opens update PRs weekly.
- **Secrets:** gitleaks scans the commits for passwords and keys.
- **CodeQL:** security analysis of the C# code (`codeql.yml`).
