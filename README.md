# Mandi Dost – backend API

ASP.NET Core 10 Web API with EF Core 10 and Microsoft SQL Server. It serves the Mandi Dost mobile app (a separate repo, `mobile`).

## Run it

Requirements: .NET 10 SDK and Microsoft SQL Server (LocalDB, Express or full; LocalDB comes with Visual Studio).

```bash
cd src/MandiDost.Api
# one time: the admin login lives in user-secrets, never in the repo
dotnet user-secrets set "Seed:AdminPhone" "03XXXXXXXXX"
dotnet user-secrets set "Seed:AdminPassword" "choose-a-strong-password"
dotnet run --launch-profile http     # http://localhost:5080 (this PC only)
dotnet run --launch-profile lan      # reachable from phones on the same Wi-Fi
```

On Windows you can also double-click `start-backend.cmd`.

**Database connection, one per environment:**

| Environment | Where | Database |
|---|---|---|
| Development (your PC) | `appsettings.Development.json` | `(localdb)\MSSQLLocalDB`, database `MandiDost`, Windows login (no password) |
| Production (live server) | `appsettings.Production.json` | `.\MSSQLSERVER2022`, database `MandiDostDB`, user `MandiDost` |

The production password is not in the repo: on the server it is the `Database__Password` setting in `web.config` (see `deploy/DEPLOY.md`).

- In Development the API creates and updates the database when it starts. In production it does not: run `deploy/mandidost-database.sql` once instead.
- API explorer: http://localhost:5080/scalar. Health check: http://localhost:5080/health
- Tests (SQLite in memory, no SQL Server needed): `dotnet test`

## Production notes

- Set `Database:Password`, `Jwt:SigningKey` (32+ characters), `Seed:*`, `Voice:ApiKey` (if used) and `Cors:AllowedOrigins` through environment variables. Never commit them.
- `appsettings.Production.json` turns off auto-migration and uses an empty CORS list.
- Uploaded photos and voice notes are stored under `App_Data/media` (or `Media:RootPath`) and served at `/media`. Back this folder up with the database.
- Behind nginx or IIS the API trusts `X-Forwarded-*` headers from the same machine only.

## Voice recognition (optional)

`POST /api/voice/transcribe` turns a recording into text through any speech service that offers the OpenAI-style
`audio/transcriptions` API (OpenAI, Groq, …). It stays switched off until a key is set, and the app then falls back
to typing or the phone keyboard's own microphone. Set it with user-secrets or environment variables, never in a file:

```bash
dotnet user-secrets set "Voice:ApiKey" "your-key"
# optional: another provider or model, e.g. Groq
dotnet user-secrets set "Voice:BaseUrl" "https://api.groq.com/openai/v1"
dotnet user-secrets set "Voice:Model" "whisper-large-v3"
```

On a server use `Voice__ApiKey`, `Voice__BaseUrl`, `Voice__Model`. Each user can send 20 recordings per 10 minutes.
Urdu works best; check Punjabi and Saraiki with real voices before promising them.
`POST /api/voice/parse` (understanding a sentence) needs no key and no outside service.

## Endpoints

| Endpoint | What it does |
|---|---|
| `POST /api/auth/register` | Sign up with a Pakistani mobile number and password |
| `POST /api/auth/login` | Log in; the account locks for 15 minutes after 5 wrong attempts |
| `POST /api/auth/refresh` | Get a new access token (rotating refresh tokens) |
| `POST /api/auth/logout` | End this session |
| `GET/PATCH /api/users/me` | Your profile (name, city, bio, WhatsApp number, language) |
| `PUT /api/users/me/password` | Change password (signs out all devices) |
| `GET /api/categories`, `GET /api/locations` | Reference data (public) |
| `GET /api/listings` | Search published listings (`section`, `purpose`, `category`, `locationId`, `lat`/`lng`/`radiusKm`, price, `gender`, `teeth`, weight, `shares`, `bulk`, `q`, `sort`) |
| `GET /api/listings/{id}` | One listing with all its details |
| `POST /api/listings`, `PUT /api/listings/{id}` | Create (as a draft) or update a listing |
| `POST /api/listings/{id}/media` | Upload a photo (JPEG/PNG/WebP, up to 5 MB, up to 8 per listing) |
| `POST /api/listings/{id}/publish`, `/sold`, `DELETE /api/listings/{id}` | Publish for 60 days (or renew), mark as sold, remove |
| `GET /api/users/me/listings`, `GET/PUT/DELETE /api/users/me/favorites/{id}` | My listings, favourites |
| `GET/PUT /api/users/me/business`, `POST …/business/verification` | Business profile and verification request |
| `GET /api/purchase-requests`, `GET /api/purchase-requests/{id}` | Open buyer requirements |
| `POST /api/purchase-requests`, `PUT …/{id}`, `POST …/{id}/close`, `POST …/{id}/reopen` | Post and manage a requirement |
| `POST /api/purchase-requests/{id}/offers` | Make or change a seller offer |
| `POST /api/offers/{id}/accept`, `/decline`, `/withdraw` | Answer or withdraw an offer |
| `GET /api/users/me/purchase-requests`, `GET /api/users/me/offers` | My requirements, my offers |
| `GET /api/chats`, `POST /api/chats`, `GET /api/chats/{id}`, `POST /api/chats/{id}/messages`, `POST /api/chats/{id}/voice` | Chat with text and voice messages (the app polls with `afterId`) |
| `GET /api/listings/{id}/shares`, `POST /api/listings/{id}/bookings`, `POST /api/bookings/{id}/confirm`, `/decline`, `/cancel` | Qurbani 7-share bookings |
| `GET /api/users/{id}`, `GET/POST /api/users/{id}/reviews`, `POST /api/listings/{id}/reports` | Seller page, reviews, reports |
| `GET /api/notifications` (`afterId` to poll, `beforeId` for older), `GET /api/notifications/unread-count`, `POST /api/notifications/{id}/read`, `POST /api/notifications/read-all` | In-app alerts: nearby listings and requirements, offers, share bookings. Max 20 nearby alerts per user per day |
| `GET/PUT /api/users/me/notification-settings` | On/off, radius (5–300 km), alert kinds, categories |
| `POST /api/voice/parse`, `GET /api/voice/status`, `POST /api/voice/transcribe` | Understand a spoken or typed Urdu sentence (sell / wanted / search and the fields in it); say whether recordings can be transcribed; recording → text |
| `GET /api/admin/stats`, `GET /api/admin/businesses/pending`, `POST /api/admin/businesses/{id}/verification`, `GET /api/admin/reports`, `POST /api/admin/reports/{id}/resolve` | Admin and moderator tools |
