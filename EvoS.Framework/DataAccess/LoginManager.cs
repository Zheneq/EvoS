using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EvoS.DirectoryServer.ARLauncher;
using EvoS.Framework;
using EvoS.Framework.Auth;
using EvoS.Framework.DataAccess;
using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Static;
using log4net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace EvoS.DirectoryServer.Account
{
    public class LoginManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LoginManager));

        // PBKDF2-HMAC-SHA512 password hasher (ASP.NET Core Identity v3 format). The iteration count is
        // raised above the framework default (100k) to meet OWASP's guidance for SHA512; it is embedded in
        // each hash, so Identity transparently re-hashes on the next login when this value is increased.
        private static readonly PasswordHasher<object> passwordHasher = new PasswordHasher<object>(
            Options.Create(new PasswordHasherOptions { IterationCount = 210_000 }));

        // Marks hashes produced by the current (KDF-based) scheme. Anything without this prefix is a legacy
        // single-pass SHA-256 hash that is verified and then upgraded on successful login.
        private const string HashV2Prefix = "v2:";
        // Insecure default value of the password pepper (Database.Salt); rejected outside of DevMode.
        private const string DefaultPepper = "salt";

        private static readonly Regex usernameRegex = new Regex(@"^[A-Za-z][A-Za-z_\-0-9]{3,23}$");
        private static readonly Regex bannedUsernameRegex = new Regex(@"^(?:(?:changeMeToYour)?user(?:name)?|admin|draft|gaia|maps)$", RegexOptions.IgnoreCase);
        private static readonly Regex bannedPasswordRegex = new Regex(@"^(?:(?:changeMeToYour)?password)$", RegexOptions.IgnoreCase);

        public const string PasswordIsIncorrect = "Password is incorrect";
        public const string UserDoesNotExist = "User does not exist";
        public const string InvalidUsername = "Invalid username. " +
                        "Please use only basic latin characters, numbers, underscore and dash, and start with a letter. " +
                        "Between 4 and 24 symbols.";
        public const string CannotUseThisUsername = "You cannot use this username. Please choose another.";
        public const string CannotUseThisPassword = "You cannot use this password. Please choose another.";
        public const string PasswordTooShort = "Password is too short. It must be at least {0} characters. Please choose another.";
        public const string FailedToCreateAnAccount = "Failed to crate an account";
        public const string UserNotFound = "User not found";
        public const string LinkedAccountNotFound = "This third-party account is not linked to this account.";
        public const string SteamIdMissing = "Account lacks SteamId. Please use ARLauncher to link your account to Steam.";
        public const string SteamIdZero = "No SteamId was provided. Please use ARLauncher to create an account or to link existing account to Steam.";
        public const string SteamIdAlreadyUsed = "Provided SteamId was already used for another account. Try logging into it instead. You can reset password if you forgot it.";
        public const string SteamWebApiKeyMissing = "Server is not configured to use SteamWebApi";
        public const string AccountWithSuchLinkedAccountNotFound = "Account linked to this third-party account was not found";
        public const string AccountTypeNotSuitableForPasswordReset = "Third-party account you have logged in with cannot be used for password reset";
        public const string UsernameIsAlreadyUsed = "This username is already in use. Please, choose another.";
        public const string TooManyLinkedAccounts = "You cannot link so many third-party accounts.";
        public const string InsufficientTrustLevel =
            "Unfortunately, provided third-party accounts do not match the required trust level. Please, try linking other accounts, or contact support.";
        public const string RegistrationCodeNeeded = "A registration code is required to get access to this server.";
        public const string RegistrationCodeInvalid = "Your registration code is not valid or already used.";
        public const string RegistrationCodeWrongUsername = "Your username does not match the one you have requested.";
        public const string RegistrationCodeExpired = "Your registration code has expired. Please, request a new one.";

        public static long RegisterOrLogin(AuthInfo authInfo)
        {
            LoginDao.LoginEntry entry = DB.Get().LoginDao.Find(authInfo.UserName.ToLower());
            return entry != null
                ? Login(authInfo.UserName, authInfo._Password)
                : AutoRegister(authInfo);
        }

        private static long AutoRegister(AuthInfo authInfo)
        {
            if (!EvosConfiguration.GetAutoRegisterNewUsers())
            {
                log.Info($"Attempt to login as \"{authInfo.UserName}\"");
                throw new ArgumentException(UserNotFound);
            }

            log.Info($"Registering user automatically: {authInfo.UserName}");
            return Register(authInfo.UserName, authInfo._Password);
        }

        public static long Register(
            string username,
            string password,
            string code = null,
            List<LinkedAccount.Ticket> linkedAccountTickets = null,
            bool ignoreConditions = false)
        {
            LoginDao loginDao = DB.Get().LoginDao;
            LoginDao.LoginEntry entry = loginDao.Find(username.ToLower());

            if (entry is not null)
            {
                log.Info($"Attempt to register as existing user \"{username}\"");
                throw new ConflictException(UsernameIsAlreadyUsed);
            }
            
            if (!IsValidUsername(username))
            {
                log.Info($"Attempt to register as \"{username}\"");
                throw new ArgumentException(InvalidUsername);
            }

            if (!IsAllowedUsername(username) && !ignoreConditions)
            {
                log.Info($"Attempt to register as \"{username}\"");
                throw new ArgumentException(CannotUseThisUsername);
            }

            ValidatePassword(password, ignoreConditions ? 0 : EvosConfiguration.GetMinPasswordLength());

            List<LinkedAccount> linkedAccounts = ProcessLinkedAccountTickets(linkedAccountTickets);
            if (!ignoreConditions)
            {
                ValidateLinkedAccountConditions(EvosConfiguration.GetLinkedAccountRegistrationConditions(), linkedAccounts);
            }
            
            if (linkedAccounts.Count > EvosConfiguration.GetMaxLinkedAccounts())
            {
                throw new ArgumentException(TooManyLinkedAccounts);
            }

            RegistrationCodeDao.RegistrationCodeEntry registrationCodeEntry = null;
            RegistrationCodeDao registrationCodeDao = DB.Get().RegistrationCodeDao;
            if (EvosConfiguration.GetRequireRegistrationCode() && !ignoreConditions)
            {
                if (code is null)
                {
                    throw new ArgumentException(RegistrationCodeNeeded);
                }

                RegistrationCodeDao.RegistrationCodeEntry e = registrationCodeDao.Find(code);
                if (e is not null && !e.IsUsed && e.HasExpired)
                {
                    throw new ArgumentException(RegistrationCodeExpired);
                }
                if (e is not null && e.IsValid && !e.IssuedTo.Equals(username.ToLower()))
                {
                    throw new ArgumentException(RegistrationCodeWrongUsername);
                }
                if (e is null || !e.IsValid || !e.IssuedTo.Equals(username.ToLower()))
                {
                    throw new ArgumentException(RegistrationCodeInvalid);
                }

                registrationCodeEntry = e;
            }
            else if (code is not null)
            {
                // consume the registration code if it exists even if it is not required
                RegistrationCodeDao.RegistrationCodeEntry e = registrationCodeDao.Find(code);
                if (e is not null && e.IsValid && e.IssuedTo.Equals(username.ToLower()))
                {
                    registrationCodeEntry = e;
                }
            }
            
            long accountId = GenerateAccountId();
            for (int i = 0; loginDao.Find(accountId) != null; ++i)
            {
                accountId++;
                if (i >= 100)
                {
                    log.Error($"Failed to register new user {username}");
                    throw new EvosException(FailedToCreateAnAccount);
                }
            }

            PersistedAccountData account = CreateAccount(accountId, username);
            if (account is null)
            {
                throw new EvosException(FailedToCreateAnAccount);
            }
            
            SaveLogin(accountId, username, password, linkedAccounts);
            if (registrationCodeEntry is not null)
            {
                registrationCodeDao.Save(registrationCodeEntry.Use(accountId));
            }
            log.Info($"Successfully registered new user {accountId}/{username}");
            return accountId;
        }

        private static List<LinkedAccount> ProcessLinkedAccountTickets(
            List<LinkedAccount.Ticket> linkedAccountTickets,
            long allowDisabledAccountLinkedToAccountId = 0)
        {
            if (linkedAccountTickets is null)
            {
                return new List<LinkedAccount>();
            }
            
            List<LinkedAccount> linkedAccounts = linkedAccountTickets.Select(CheckLinkedAccountTicket).ToList();
            foreach (LinkedAccount linkedAccount in linkedAccounts)
            {
                LoginDao.LoginEntry existingAccount = DB.Get().LoginDao.FindByLinkedAccount(linkedAccount);
                if (existingAccount != null
                    && (allowDisabledAccountLinkedToAccountId == 0
                        || existingAccount.AccountId != allowDisabledAccountLinkedToAccountId
                        || (existingAccount.GetLinkedAccount(linkedAccount)?.Active ?? true)))
                {
                    log.Info(
                        $"Won't allow creating account with {linkedAccount.Type} already linked to {existingAccount.Username}/{existingAccount.AccountId}");
                    throw new ArgumentException(
                        $"This {linkedAccount.Type} account is already linked to an existing Atlas Reactor account. Try logging into it instead.");
                }
            }
            return CheckLinkedAccountLevels(linkedAccounts);
        }

        private static void ValidateLinkedAccountConditions(List<List<LinkedAccount.Condition>> conditions, List<LinkedAccount> linkedAccounts)
        {
            foreach (List<LinkedAccount.Condition> condition in conditions)
            {
                if (!condition.Any(c => c.Matches(linkedAccounts)))
                {
                    if (!condition.Any(c => c.Matches(linkedAccounts, true)))
                    {
                        throw new ArgumentException(
                            $"You need to link one of the following third-party accounts: {string.Join(" or ", condition)}");
                    }
                    else
                    {
                        throw new ArgumentException(InsufficientTrustLevel);
                    }
                }
            }
        }

        private static LinkedAccount CheckLinkedAccountTicket(LinkedAccount.Ticket ticket)
        {
            switch (ticket.Type)
            {
                case LinkedAccount.AccountType.STEAM:
                    SteamWebApiConnector.Response steamResponse = Task.Run(() => SteamWebApiConnector.Instance.GetSteamIdAsync(ticket.Token)).GetAwaiter().GetResult();
                    if (steamResponse.ResultCode != SteamWebApiConnector.GetSteamIdResult.Success || steamResponse.SteamId == 0UL)
                    {
                        log.Warn($"Failed to verify Steam account: {steamResponse.ResultCode} {steamResponse.SteamId}");
                        throw new EvosException("Failed to verify Steam account");
                    }

                    return new LinkedAccount(
                        LinkedAccount.AccountType.STEAM,
                        steamResponse.SteamId.ToString(),
                        steamResponse.SteamId.ToString(),
                        0,
                        DateTime.MinValue,
                        true);
                default:
                    throw new ArgumentException($"{ticket.Type} account type is not supported");
            }
        }

        private static List<LinkedAccount> CheckLinkedAccountLevels(List<LinkedAccount> linkedAccounts)
        {
            // TODO check linked account levels, pull usernames
            return linkedAccounts;
        }

        public static PersistedAccountData CreateAccount(long accountId, string username)
        {
            DB.Get().AccountDao.CreateAccount(AccountManager.CreateAccount(accountId, username));
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);
            if (account == null)
            {
                log.Error($"Error creating a new account for player '{username}'/{accountId}");
            }

            return account;
        }

        private static void SaveLogin(long accountId, string username, string password, List<LinkedAccount> linkedAccounts)
        {
            LoginDao loginDao = DB.Get().LoginDao;
            string hash = HashV2(password);
            loginDao.Save(new LoginDao.LoginEntry
            {
                AccountId = accountId,
                // The per-user salt is embedded in the v2 hash; this field is kept only for verifying
                // and upgrading pre-existing legacy (SHA-256) hashes.
                Salt = string.Empty,
                Hash = hash,
                Username = username.ToLower(),
                LinkedAccounts = linkedAccounts,
            });
            log.Info($"Successfully generated new password hash for {accountId}/{username}");
        }

        public static long Login(string username, string password)
        {
            LoginDao.LoginEntry entry = DB.Get().LoginDao.Find(username.ToLower());
            if (entry == null)
            {
                log.Warn($"Attempt to log is as non-existing user {username}");
                throw new ArgumentException(UserNotFound);
            }

            bool passwordMatches = VerifyPassword(entry.Hash, entry.Salt, password, out bool needsRehash);
            bool usedTempPassword = false;
            if (!passwordMatches)
            {
                if (!entry.TempPassword.IsNullOrEmpty()
                    && entry.TempPasswordTimeout > DateTime.UtcNow
                    && VerifyPassword(entry.TempPassword, entry.Salt, password, out _))
                {
                    log.Warn($"{entry.AccountId}/{entry.Username} logged in using temporary password");
                    ClearTempPassword(entry.AccountId);
                    // Also clear on the local copy so the Save below does not resurrect the temp password.
                    entry.TempPassword = string.Empty;
                    entry.TempPasswordTimeout = DateTime.MinValue;
                    usedTempPassword = true;
                }
                else
                {
                    log.Warn($"Failed attempt to log in as {entry.AccountId}/{entry.Username}");
                    throw new ArgumentException(PasswordIsIncorrect);
                }
            }

            List<LinkedAccount> linkedAccounts = CheckLinkedAccountLevels(entry.LinkedAccounts);
            ValidateLinkedAccountConditions(EvosConfiguration.GetLinkedAccountLoginConditions(), linkedAccounts);

            entry.LinkedAccounts = linkedAccounts;
            DB.Get().LoginDao.Save(entry);

            log.Info($"User {entry.AccountId}/{entry.Username} successfully logged in");
            // Transparently upgrade legacy or outdated password hashes on a successful real-password login.
            // Never rehash from a temporary password - that would overwrite the real password.
            if (!usedTempPassword && needsRehash)
            {
                UpdatePassword(entry, password);
            }
            return entry.AccountId;
        }

        public static void LinkAccounts(long accountId, List<LinkedAccount.Ticket> tickets)
        {
            List<LinkedAccount> linkedAccounts = ProcessLinkedAccountTickets(tickets, accountId);
            
            var loginDao = DB.Get().LoginDao;
            var entry = loginDao.Find(accountId);
            if (entry is null)
            {
                throw new ArgumentException(UserNotFound);
            }

            List<LinkedAccount> accounts = entry.LinkedAccounts.Where(la => !linkedAccounts.Any(la.IsSame)).ToList();
            accounts.AddRange(linkedAccounts); // TODO multiple accounts of the same type?

            if (accounts.Count > EvosConfiguration.GetMaxLinkedAccounts())
            {
                throw new ArgumentException(TooManyLinkedAccounts);
            }

            entry.LinkedAccounts = accounts;
            loginDao.Save(entry);
        }

        public static void DisableLink(long accountId, LinkedAccount linkedAccount)
        {
            var loginDao = DB.Get().LoginDao;
            var entry = loginDao.Find(accountId);
            if (entry is null)
            {
                throw new ArgumentException(UserNotFound);
            }

            LinkedAccount linkedAccountToDisable = entry.GetLinkedAccount(linkedAccount);

            if (linkedAccountToDisable is null)
            {
                throw new ArgumentException(LinkedAccountNotFound);
            }

            linkedAccountToDisable.Active = false;
            loginDao.Save(entry);
        }

        // TODO API?
        public static string RemindUsername(LinkedAccount.Ticket ticket)
        {
            LinkedAccount linkedAccount = CheckLinkedAccountTicket(ticket);
            var entry = DB.Get().LoginDao.FindByLinkedAccount(linkedAccount);
            if (entry == null)
            {
                throw new ArgumentException(AccountWithSuchLinkedAccountNotFound);
            }
            return entry.Username;
        }

        public static void ResetPassword(LinkedAccount.Ticket ticket, string newPassword)
        {
            if (!EvosConfiguration.GetLinkedAccountsForPasswordReset().Contains(ticket.Type))
            {
                throw new ArgumentException(AccountTypeNotSuitableForPasswordReset);
            }
            LinkedAccount linkedAccount = CheckLinkedAccountTicket(ticket);
            var loginDao = DB.Get().LoginDao;
            var entry = loginDao.FindByLinkedAccount(linkedAccount);
            if (entry == null)
            {
                throw new ArgumentException(AccountWithSuchLinkedAccountNotFound);
            }

            ResetPassword(entry.AccountId, newPassword);
        }
        
        public static void ResetPassword(long accountId, string newPassword)
        {
            ValidatePassword(newPassword, EvosConfiguration.GetMinPasswordLength());
            var loginDao = DB.Get().LoginDao;
            var entry = loginDao.Find(accountId);
            if (entry == null)
            {
                throw new ArgumentException(UserNotFound);
            }

            UpdatePassword(entry, newPassword);
        }

        private static void UpdatePassword(LoginDao.LoginEntry entry, string newPassword)
        {
            SaveLogin(entry.AccountId, entry.Username, newPassword, entry.LinkedAccounts);
        }

        private static long GenerateAccountId()
        {
            byte[] bytes = RandomNumberGenerator.GetBytes(8);
            long value = BitConverter.ToInt64(bytes, 0) & long.MaxValue;
            return value % 9_000_000_000_000_000L + 1_000_000_000_000_000L;
        }

        /// <summary>
        /// Applies the server-side pepper (Database.Salt) as an HMAC key over the password before it is handed
        /// to the KDF. Keeping the pepper out of the database means a database-only leak cannot be cracked
        /// offline without also compromising the server configuration.
        /// </summary>
        private static string PreparePassword(string password)
        {
            byte[] pepper = Encoding.UTF8.GetBytes(EvosConfiguration.GetDBConfig().Salt ?? string.Empty);
            byte[] mac = HMACSHA512.HashData(pepper, Encoding.UTF8.GetBytes(password ?? string.Empty));
            return Convert.ToBase64String(mac);
        }

        /// <summary>
        /// Hashes a password with the current KDF-based scheme (peppered, PBKDF2 via Identity), tagged so it
        /// can be told apart from legacy hashes.
        /// </summary>
        internal static string HashV2(string password)
        {
            return HashV2Prefix + passwordHasher.HashPassword(null!, PreparePassword(password));
        }

        /// <summary>
        /// Verifies a password against a stored hash, supporting both the current scheme and legacy SHA-256
        /// hashes. <paramref name="needsRehash"/> is set when the stored hash should be replaced with a fresh
        /// one - always for legacy hashes, and when the KDF parameters have since been strengthened.
        /// </summary>
        internal static bool VerifyPassword(string storedHash, string legacySalt, string password, out bool needsRehash)
        {
            needsRehash = false;
            if (storedHash.IsNullOrEmpty())
            {
                return false;
            }

            if (storedHash.StartsWith(HashV2Prefix))
            {
                PasswordVerificationResult result = passwordHasher.VerifyHashedPassword(
                    null!, storedHash.Substring(HashV2Prefix.Length), PreparePassword(password));
                needsRehash = result == PasswordVerificationResult.SuccessRehashNeeded;
                return result != PasswordVerificationResult.Failed;
            }

            // Legacy single-pass SHA-256 hash: verify, and flag for upgrade to the current scheme.
            if (LegacyHash(legacySalt, password).Equals(storedHash))
            {
                needsRehash = true;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Reproduces the original SHA-256 hashing scheme, used only to verify pre-existing hashes so they can
        /// be upgraded on login. Not thread-affine: uses the stateless <see cref="SHA256.HashData(byte[])"/>.
        /// </summary>
        internal static string LegacyHash(string customSaltPart, string password)
        {
            if (customSaltPart.IsNullOrEmpty()) customSaltPart = string.Empty;
            byte[] bytes = Encoding.UTF8.GetBytes(EvosConfiguration.GetDBConfig().Salt + customSaltPart + password);
            byte[] hashBytes = SHA256.HashData(bytes);
            StringBuilder sb = new StringBuilder();
            foreach (byte b in hashBytes)
            {
                sb.Append(b.ToString("X2"));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Fails fast at startup if the password pepper (Database.Salt) is unset or left at the insecure
        /// default. Logs a warning instead of throwing when DevMode is enabled.
        /// </summary>
        public static void ValidateConfiguration()
        {
            string pepper = EvosConfiguration.GetDBConfig().Salt;
            if (!pepper.IsNullOrEmpty() && pepper != DefaultPepper)
            {
                return;
            }

            const string msg = "Database.Salt (password pepper) is unset or left at the insecure default. " +
                               "Set it to a strong, unique secret stored separately from the database.";
            if (EvosConfiguration.GetDevMode())
            {
                log.Warn($"INSECURE CONFIGURATION: {msg} Continuing because DevMode is enabled.");
            }
            else
            {
                throw new EvosException(msg);
            }
        }

        private static string GenerateSalt()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        }

        public static string GenerateApiKey()
        {
            return GenerateSalt();
        }

        private static string GeneratePassword()
        {
            string rnd = Convert.ToBase64String(RandomNumberGenerator.GetBytes(8));
            return rnd.Substring(0, rnd.Length - 1);
        }

        public static string GenerateTempPassword(long accountId)
        {
            LoginDao loginDao = DB.Get().LoginDao;
            LoginDao.LoginEntry loginEntry = loginDao.Find(accountId);
            if (loginEntry is null)
            {
                return string.Empty;
            }

            string tempPassword = GeneratePassword();
            string hash = HashV2(tempPassword);
            loginEntry.TempPassword = hash;
            loginEntry.TempPasswordTimeout = DateTime.UtcNow + EvosConfiguration.GetTempPasswordLifetime();
            loginDao.Save(loginEntry);
            log.Info($"Successfully generated temporary password hash for {accountId}");
            return tempPassword;
        }

        public static bool ClearTempPassword(long accountId)
        {
            LoginDao loginDao = DB.Get().LoginDao;
            LoginDao.LoginEntry loginEntry = loginDao.Find(accountId);
            if (loginEntry is null)
            {
                return false;
            }
            
            loginEntry.TempPassword = string.Empty;
            loginEntry.TempPasswordTimeout = DateTime.MinValue;
            loginDao.Save(loginEntry);
            log.Info($"Successfully cleared temporary password for {accountId}");
            return true;
        }

        public static void RevokeActiveTickets(long accountId)
        {
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);
            if (account is null)
            {
                throw new ArgumentException(UserNotFound);
            }

            account.ApiKey = GenerateApiKey();
            DB.Get().AccountDao.UpdateAccount(account);
        }

        public static bool IsValidUsername(string username)
        {
            return usernameRegex.IsMatch(username);
        }

        public static bool IsAllowedUsername(string username)
        {
            return IsValidUsername(username)
                   && !bannedUsernameRegex.IsMatch(username);
        }

        /// <summary>
        /// Enforces the password policy on registration and reset: banned passwords are always rejected, and
        /// when <paramref name="minLength"/> is positive, passwords shorter than it are rejected.
        /// </summary>
        internal static void ValidatePassword(string password, int minLength)
        {
            if (password is null || bannedPasswordRegex.IsMatch(password))
            {
                log.Info("Attempt to use a banned password");
                throw new ArgumentException(CannotUseThisPassword);
            }

            if (minLength > 0 && password.Length < minLength)
            {
                log.Info("Attempt to use a password shorter than the configured minimum");
                throw new ArgumentException(string.Format(PasswordTooShort, minLength));
            }
        }
    }
}