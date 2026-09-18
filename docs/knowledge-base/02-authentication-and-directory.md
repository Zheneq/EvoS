# 02 – Authentication & Directory

## Responsibilities

Authenticate a game client and hand it the lobby address plus session tokens. Also: account
registration, password hashing, linked third-party accounts (Steam), registration codes,
and JWT-based auth for the REST APIs and launcher tickets.

## Key classes

| Class | Location | Notes |
|-------|----------|-------|
| `DirectoryServer` / `Program` | `EvoS.DirectoryServer/DirectoryServer.cs` | The whole HTTP endpoint: one `app.Run` lambda, JSON in/out |
| `LoginManager` | `EvoS.Framework/DataAccess/LoginManager.cs` | **Namespace `EvoS.DirectoryServer.Account` despite living in Framework/DataAccess.** Registration, password verify/upgrade, linked accounts, registration codes |
| `EvosAuth` | `EvoS.Framework/Auth/EvosAuth.cs` | JWT create/validate (HMAC-SHA512), separate `Context`s: TICKET_AUTH, ADMIN_API, USER_API |
| `AuthTicket` | `EvoS.Framework/Auth/AuthTicket.cs` | Launcher auth-ticket format (XML-ish), parsed on directory login when `AllowTicketAuth` |
| `SessionTicketData` | `LobbyServer2/LobbyServer/Session/SessionTicketData.cs` | Reconnection ticket (accountId + session token + reconnect token) |
| `GameServerAuth` | `EvoS.Framework/Auth/GameServerAuth.cs` | Nonce challenge / signature verification for game-server registration (see doc 04) |
| `SteamWebApiConnector` | `EvoS.Framework/Auth/SteamWebApiConnector.cs` | Steam ticket validation for linked accounts |

## Login flow (`DirectoryServer.ProcessRequest`)

1. Protocol version gate (`ProtocolVersion.SUPPORTED_PROTO_VERSIONS`).
2. If the request carries a ticket:
   - Try `SessionTicketData` → reconnection path (`HandleReconnection`): validates both session
     token and reconnect token against `SessionManager` live/disconnected sessions.
   - Else if ticket auth enabled, try `AuthTicket` → `HandleConnectionWithToken`: JWT validation
     + account-id match + IP address check (loopback exemptions).
3. Fallback: username/password → `LoginManager.RegisterOrLogin`
   (auto-registration if `AutoRegisterNewUsers`).
4. `HandleConnection`:
   - Fast-path concurrent-login rejection, then authoritative atomic check inside
     `SessionManager.CreateSession(rejectIfActive: true)` (throws `ConflictException`).
   - Loads or creates the account; **on any DB exception falls back to creating a temp
     account** (`temp_user#N`) — questionable resilience-over-correctness choice.
   - `PatchAccountData`: in-login data migration (placeholder-character loadouts,
     free-store unlocks, Trust War init). Applies patches then reports whether the account
     actually changed (JSON snapshot comparison), so the DB write is skipped on the common
     no-op path. Covered by `Tests/PatchAccountDataTest.cs`.

## Password handling (`LoginManager`)

- PBKDF2-HMAC-SHA512 via ASP.NET Identity `PasswordHasher`, 210k iterations, hashes prefixed
  `v2:`; legacy single-pass SHA-256 hashes verified then transparently upgraded on login.
- Pepper = `Database.Salt` config value; the insecure default (`"salt"`) is rejected outside
  DevMode (`ValidateConfiguration`).
- Dummy-hash verification for unknown users (timing-attack mitigation, see commit
  `01b19f8 Dummy pw verification`).
- Username validation regexes, banned username/password lists, min length from config.

## Trust boundaries

- Directory endpoint is anonymous by design; failed logins are rate-limited only by logging.
- `GetActualClientIpAddress` honors `ClientIpHeader` config for reverse-proxy deployments;
  proxy detection (`LobbyServer2/Proxy/`) can rewrite the advertised lobby address per client.
- REST APIs authenticate via JWTs signed with `AdminApiKey` / `UserApiKey` (doc 09); key
  strength is enforced at startup (`EvosConfiguration.ValidateApiKeyStrength`, min 32 chars).
