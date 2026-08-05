# Alliance MarkUP

A web-based academic management system with role-based portals for **Admins**, **Teachers**, and **Students** — covering enrollment, course/curriculum assignment, grading, class scheduling, and notifications.

Built with ASP.NET Core MVC (.NET 9) on top of the ASI Basecode architecture (a layered `Data` / `Services` / `Resources` / `WebApp` structure).

## Features

**Admin**
- Dashboard with academic overview/reports
- Manage accounts (students, teachers, admins)
- Assign teachers to courses and course loads
- Manage programs, courses, and curriculum
- View student and teacher profiles

**Teacher**
- Dashboard with assigned courses
- View and manage courses taught
- Profile management

**Student**
- Dashboard with study load
- View grades
- Profile management

**Shared**
- Calendar with events
- Notifications
- Email-based password reset (SMTP)
- JWT + cookie-based authentication

## Tech Stack

- **Backend:** ASP.NET Core MVC (.NET 9), Entity Framework Core
- **Database:** PostgreSQL (via Npgsql), hosted on Supabase
- **Auth:** JWT bearer + cookie authentication
- **Other:** Hangfire (background jobs), CsvHelper, ZXing.Net (QR/barcode), AutoMapper

## Project Structure

```
ASI.Basecode.Data/       Repositories, EF Core models, migrations, DB context
ASI.Basecode.Services/   Business logic and services consumed by the WebApp
ASI.Basecode.Resources/  Shared messages, labels, and translations
ASI.Basecode.WebApp/     Controllers, Views, and application startup/config
```

The layering is intentional:
- Repositories (`Data`) only handle database access — no business logic.
- Business logic lives in `Services`, which the `WebApp` layer consumes.
- Static/user-facing text lives in `Resources`, not hardcoded in views or controllers.

## Getting Started

### Prerequisites
- [Visual Studio 2022](https://visualstudio.microsoft.com/) (or the .NET 9 SDK + your editor of choice)
- .NET 9 SDK
- A PostgreSQL database (e.g. a free [Supabase](https://supabase.com/) project)

### Setup

1. Clone the repo:
   ```bash
   git clone <repo-url>
   cd AllianceMarkUP
   ```
2. Open `ASI.Basecode.sln` in Visual Studio 2022.
3. Configure your local secrets instead of editing `appsettings.json` directly — right-click **ASI.Basecode.WebApp** → **Manage User Secrets**, and add:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=...;Port=5432;Database=...;Username=...;Password=...;Pooling=true;SSL Mode=Require"
     },
     "TokenAuthentication": {
       "SecretKey": "your-own-secret-key",
       "Audience": "AsiBasecodAudience",
       "TokenPath": "/api/token",
       "CookieName": "tkn",
       "ExpirationMinutes": "120"
     },
     "Smtp": {
       "Host": "smtp.gmail.com",
       "Port": "587",
       "User": "your-email@example.com",
       "Pass": "your-app-password",
       "From": "your-email@example.com"
     }
   }
   ```
4. Set **ASI.Basecode.WebApp** as the startup project.
5. Apply migrations / update the database (via EF Core Package Manager Console or `dotnet ef database update`).
6. Clean and rebuild the solution, then run.

> ⚠️ **Security note:** `appsettings.json` should never contain real credentials in source control. Use user secrets locally and environment variables / a secrets manager in production.

## Roadmap / Ideas

- [ ] Move secrets out of `appsettings.json` entirely
- [ ] Add automated tests
- [ ] CI/CD pipeline

## License

_Add a license (e.g. MIT) if you intend to make this repo public._
