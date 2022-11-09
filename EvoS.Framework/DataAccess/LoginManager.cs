using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using EvoS.Framework;
using EvoS.Framework.DataAccess;
using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Network.Static;
using log4net;

namespace EvoS.DirectoryServer.Account
{
    public class LoginManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LoginManager));
        private static readonly HashAlgorithm algorithm = SHA256.Create();
        private static readonly Regex re = new Regex(@"^[A-Za-z][A-Za-z_\-0-9]{3,}$");

        public static long RegisterOrLogin(AuthInfo authInfo)
        {
            LoginDao loginDao = DB.Get().LoginDao;
            LoginDao.LoginEntry entry = loginDao.Find(authInfo.UserName);
            string hash = Hash(authInfo._Password);
            if (entry != null)
            {
                if (entry.Hash.Equals(hash))
                {
                    log.Info($"User {entry.AccountId}/{entry.Username} successfully logged in");
                    return entry.AccountId;
                }
                else
                {
                    log.Warn($"Failed attempt to log is as {entry.AccountId}/{entry.Username}");
                    throw new ArgumentException("Password is incorrect");
                }
            }
            else
            {
                if (!re.IsMatch(authInfo.UserName))
                {
                    log.Info($"Attempt to register as \"{authInfo.UserName}\"");
                    throw new ArgumentException("Invalid username. " + 
                        "Please use only latin characters, numbers, underscore and dash, and start with a letter. " +
                        "4 symbols or more.");
                }
                long accountId = GenerateAccountId(authInfo.UserName);
                for (int i = 0; loginDao.Find(accountId) != null; ++i)
                {
                    accountId++;
                    if (i >= 100)
                    {
                        log.Error($"Failed to register new user {authInfo.UserName}");
                        throw new ApplicationException("Failed to crate an account");
                    }
                }
                loginDao.Save(new LoginDao.LoginEntry
                {
                    AccountId = accountId,
                    Hash = hash,
                    Username = authInfo.UserName
                });
                log.Info($"Successfully registered new user {accountId}/{authInfo.UserName}");
                return accountId;
            }
        }

        public static long Login(AuthInfo authInfo)
        {
            LoginDao.LoginEntry entry = DB.Get().LoginDao.Find(authInfo.UserName);
            string hash = Hash(authInfo._Password);
            if (entry != null)
            {
                if (entry.Hash.Equals(hash))
                {
                    log.Info($"User {entry.AccountId}/{entry.Username} successfully logged in");
                    return entry.AccountId;
                }
                else
                {
                    log.Warn($"Failed attempt to log is as {entry.AccountId}/{entry.Username}");
                    throw new ArgumentException("Password is incorrect");
                }
            }
            log.Warn($"Attempt to log is as non-existing user {authInfo.UserName}");
            throw new ArgumentException("User not found");
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

        private static string Hash(string password)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(EvosConfiguration.GetDBConfig().Salt + password);
            byte[] hashBytes = algorithm.ComputeHash(bytes);
            StringBuilder sb = new StringBuilder();
            foreach (byte b in hashBytes)
            {
                sb.Append(b.ToString("X2"));
            }
            return sb.ToString();
        }
        
        
    }
}