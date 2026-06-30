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

namespace EvoS.DirectoryServer.Account
{
    public class LoginManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LoginManager));
        private static readonly HashAlgorithm algorithm = SHA256.Create();
        private static readonly Regex usernameRegex = new Regex(@"^[A-Za-z][A-Za-z_\-0-9]{3,}$");
        private static readonly Regex bannedUsernameRegex = new Regex(@"^(?:(?:changeMeToYour)?user(?:name)?|admin|draft|gaia)$", RegexOptions.IgnoreCase);
        private static readonly Regex bannedPasswordRegex = new Regex(@"^(?:(?:changeMeToYour)?password)$", RegexOptions.IgnoreCase);

        public const string PasswordIsIncorrect = "Password is incorrect";
        public const string UserDoesNotExist = "User does not exist";
        public const string InvalidUsername = "Invalid username. " +
                        "Please use only latin characters, numbers, underscore and dash, and start with a letter. " +
                        "4 symbols or more.";
        public const string CannotUseThisUsername = "You cannot use this username. Please choose another.";
        public const string CannotUseThisPassword = "You cannot use this password. Please choose another.";
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

            if (!IsAllowedUsername(username))
            {
                log.Info($"Attempt to register as \"{username}\"");
                throw new ArgumentException(CannotUseThisUsername);
            }

            if (bannedPasswordRegex.IsMatch(password))
            {
                log.Info($"Attempt to register with a bad password");
                throw new ArgumentException(CannotUseThisPassword);
            }

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
            
            long accountId = GenerateAccountId(username);
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
            string salt = GenerateSalt();
            string hash = Hash(salt, password);
            loginDao.Save(new LoginDao.LoginEntry
            {
                AccountId = accountId,
                Salt = salt,
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

            string hash = Hash(entry.Salt, password);
            if (!entry.Hash.Equals(hash))
            {
                if (entry.TempPassword.Equals(hash) && entry.TempPasswordTimeout > DateTime.UtcNow)
                {
                    log.Warn($"{entry.AccountId}/{entry.Username} logged in using temporary password");
                    ClearTempPassword(entry.AccountId);
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
            if (entry.Salt.IsNullOrEmpty())
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
            if (bannedPasswordRegex.IsMatch(newPassword))
            {
                log.Info($"Attempt to reset password with a bad newPassword");
                throw new ArgumentException(CannotUseThisPassword);
            }
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

        private static long GenerateAccountId(string a)
        {
            int num = (Guid.NewGuid() + a).GetHashCode();
            if (num < 0)
            {
                num = -num;
            }
            return num + 1000000000000000L;
        }

        private static string Hash(string customSaltPart, string password)
        {
            lock (algorithm)
            {
                if (customSaltPart.IsNullOrEmpty()) customSaltPart = string.Empty;
                byte[] bytes = Encoding.UTF8.GetBytes(EvosConfiguration.GetDBConfig().Salt + customSaltPart + password);
                byte[] hashBytes = algorithm.ComputeHash(bytes);
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    sb.Append(b.ToString("X2"));
                }
                return sb.ToString();
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
            string hash = Hash(loginEntry.Salt, tempPassword);
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
    }
}