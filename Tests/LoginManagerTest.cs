using EvoS.DirectoryServer.Account;

namespace Tests;

public class LoginManagerTest
{
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
}
