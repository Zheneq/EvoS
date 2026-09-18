using EvoS.DirectoryServer.Account;
using EvoS.Framework;

namespace Tests;

public class LoginManagerTest
{
    private const string TestPepper = "a-strong-unique-test-pepper";

    [Theory]
    [InlineData("abcd", true)]                             // 4 chars, minimum
    [InlineData("abc", false)]                             // 3 chars, too short
    [InlineData("abcdefghijklmnopqrstuvwx", true)]         // 24 chars, maximum
    [InlineData("abcdefghijklmnopqrstuvwxy", false)]       // 25 chars, too long
    [InlineData("1abc", false)]                            // must start with a letter
    [InlineData("ab cd", false)]                           // no spaces
    [InlineData("ab-c_1", true)]                           // dash, underscore, digit allowed
    public void IsValidUsername_EnforcesLengthAndCharset(string username, bool expected)
    {
        Assert.Equal(expected, LoginManager.IsValidUsername(username));
    }

    // Database.Salt is the process-wide password pepper; set it for the duration of a test and restore it.
    private static void WithPepper(string pepper, Action action)
    {
        string original = EvosConfiguration.GetDBConfig().Salt;
        EvosConfiguration.GetDBConfig().Salt = pepper;
        try
        {
            action();
        }
        finally
        {
            EvosConfiguration.GetDBConfig().Salt = original;
        }
    }

    [Fact]
    public void HashV2_RoundTrips_AndIsTaggedAndNotPlaintext()
    {
        WithPepper(TestPepper, () =>
        {
            string hash = LoginManager.HashV2("correct horse battery staple");

            Assert.StartsWith("v2:", hash);
            Assert.DoesNotContain("correct horse battery staple", hash);

            Assert.True(LoginManager.VerifyPassword(hash, "", "correct horse battery staple", out bool needsRehash));
            Assert.False(needsRehash);
            Assert.False(LoginManager.VerifyPassword(hash, "", "wrong password", out _));
        });
    }

    [Fact]
    public void Login_UnknownUser_ThrowsUserNotFound()
    {
        // Also exercises the dummy KDF verification on the missing-user path (anti-enumeration).
        WithPepper(TestPepper, () =>
        {
            ArgumentException e = Assert.Throws<ArgumentException>(
                () => LoginManager.Login("no_such_user_x1", "hunter2!"));
            Assert.Equal(LoginManager.UserNotFound, e.Message);
        });
    }

    [Fact]
    public void VerifyPassword_LegacyHash_VerifiesAndRequestsRehash()
    {
        WithPepper(TestPepper, () =>
        {
            const string salt = "c29tZS1yYW5kb20tc2FsdA==";
            string legacy = LoginManager.LegacyHash(salt, "hunter2");

            Assert.False(legacy.StartsWith("v2:"));
            Assert.True(LoginManager.VerifyPassword(legacy, salt, "hunter2", out bool needsRehash));
            Assert.True(needsRehash); // legacy hashes must be upgraded on login
            Assert.False(LoginManager.VerifyPassword(legacy, salt, "nope", out _));
        });
    }

    [Fact]
    public void VerifyPassword_EmptyHash_Fails()
    {
        Assert.False(LoginManager.VerifyPassword("", "", "whatever", out _));
        Assert.False(LoginManager.VerifyPassword(null, "", "whatever", out _));
    }

    [Fact]
    public void Pepper_ParticipatesInHash()
    {
        string hash = "";
        WithPepper("pepper-A", () => hash = LoginManager.HashV2("same-password"));

        // Same password + different pepper must not validate...
        WithPepper("pepper-B", () => Assert.False(LoginManager.VerifyPassword(hash, "", "same-password", out _)));
        // ...but validates again once the correct pepper is restored.
        WithPepper("pepper-A", () => Assert.True(LoginManager.VerifyPassword(hash, "", "same-password", out _)));
    }

    [Fact]
    public void ValidateConfiguration_ThrowsOnDefaultOrEmptyPepper()
    {
        WithPepper("salt", () => Assert.Throws<EvosException>(LoginManager.ValidateConfiguration));
        WithPepper("", () => Assert.Throws<EvosException>(LoginManager.ValidateConfiguration));
    }

    [Fact]
    public void ValidateConfiguration_PassesOnStrongPepper()
    {
        WithPepper(TestPepper, LoginManager.ValidateConfiguration);
    }

    [Fact]
    public void ValidatePassword_RejectsBannedPasswords_RegardlessOfLengthPolicy()
    {
        Assert.Throws<ArgumentException>(() => LoginManager.ValidatePassword("password", 0));
        Assert.Throws<ArgumentException>(() => LoginManager.ValidatePassword("changeMeToYourPassword", 8));
    }

    [Fact]
    public void ValidatePassword_RejectsNull()
    {
        Assert.Throws<ArgumentException>(() => LoginManager.ValidatePassword(null, 0));
    }

    [Fact]
    public void ValidatePassword_MinLengthZero_AllowsAnyLength()
    {
        // Policy disabled (default): short passwords are accepted.
        LoginManager.ValidatePassword("ab", 0);
    }

    [Fact]
    public void ValidatePassword_EnforcesMinLengthWhenSet()
    {
        Assert.Throws<ArgumentException>(() => LoginManager.ValidatePassword("short", 8));
        LoginManager.ValidatePassword("longenough12", 8); // meets the minimum
    }
}
