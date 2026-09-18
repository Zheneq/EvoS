# 09 – HTTP APIs & Admin

## API servers (`LobbyServer2/ApiServer/`)

`ApiServer` (abstract) builds a Kestrel host with JWT auth
(`EvosSecurityTokenValidator`, keys from settings.yaml), request-body buffering for
logging, Prometheus, and log4net level translation. Two concrete servers started from
`CentralServer.Init`:

| Server | Port (default) | Auth context | Consumers |
|--------|----------------|--------------|-----------|
| `AdminApiServer` | 3001 | `ADMIN_API` JWT (login via account with admin rights) | `evos.admin` React UI |
| `UserApiServer` | 3002 | `USER_API` JWT | launcher/website: login, ticket issuing (`AuthTicket` for the game client), account self-service |

Endpoints are defined as static minimal-API style controller classes:

- `StatusController` — lobby status, online players, queues, servers, games (also the
  public status feed used by community sites).
- `AdminController` — pause queue, scheduled shutdown (`PendingShutdownType`), broadcast,
  penalties (ban/mute), VIP, whispers, admin messages, registration codes, username-change
  approvals, map pick/ban, per-user detail batch queries.
- `ModerationController` — reported chat history, user feedback browsing.
- `MatchController` — match history/details/freelancer stats models (large response models).
- `GameServerKeyController` — approve/decline/revoke game-server keys (doc 04).
- `XMLResult.cs` — XML responses for game-client-facing bits.

`ApiAuthMiddleware` + `EvosAuth.Context` separate token audiences; `LogRedaction` keeps
secrets out of request logs.

## Admin web UI (`evos.admin/`)

Create-React-App TypeScript app (MUI v7, axios, react-auth-kit). Pages under
`src/components/pages` (dashboard, users, penalties, registration codes, server keys,
shutdown control...); API client in `src/lib`. Static game imagery under `public/img`.
Deployed separately; talks to the Admin API.

## Metrics

Prometheus metrics are embedded throughout via static `Metrics.CreateGauge/Summary`
(`SessionManager`, `GameManager`, `LobbyServerProtocol`, `MetricsUtils`), plus a
`KestrelMetricServer` on `MetricsPort` (1234).
