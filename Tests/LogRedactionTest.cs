using EvoS.Framework.Misc;

namespace Tests;

// Guards L2: the login request body may be logged for debugging, but credential fields must be masked.
public class LogRedactionTest
{
    [Fact]
    public void MasksPassword_KeepsOtherFields()
    {
        string json = "{\"AuthInfo\":{\"UserName\":\"bob\",\"Password\":\"s3cret!\"}}";
        string masked = LogRedaction.MaskSensitiveJsonFields(json);

        Assert.DoesNotContain("s3cret!", masked);
        Assert.Contains("\"Password\":\"***\"", masked);
        Assert.Contains("\"UserName\":\"bob\"", masked); // non-sensitive fields preserved
    }

    [Fact]
    public void MasksTicketData()
    {
        string json = "{\"AuthInfo\":{\"TicketData\":\"super-secret-ticket\"}}";
        Assert.DoesNotContain("super-secret-ticket", LogRedaction.MaskSensitiveJsonFields(json));
    }

    [Fact]
    public void HandlesEscapedQuotesInValue()
    {
        string json = "{\"Password\":\"a\\\"b\\\"c\",\"UserName\":\"x\"}";
        string masked = LogRedaction.MaskSensitiveJsonFields(json);

        Assert.DoesNotContain("a\\\"b", masked);
        Assert.Contains("\"Password\":\"***\"", masked);
        Assert.Contains("\"UserName\":\"x\"", masked);
    }

    [Fact]
    public void IsCaseInsensitiveOnKey()
    {
        string json = "{\"password\":\"secret\"}"; // JSON deserialization is case-insensitive
        Assert.DoesNotContain("secret", LogRedaction.MaskSensitiveJsonFields(json));
    }

    [Fact]
    public void DoesNotMaskSimilarlyNamedFields()
    {
        string json = "{\"PasswordHint\":\"my-dog\",\"UserPassword\":\"keep\"}";
        string masked = LogRedaction.MaskSensitiveJsonFields(json);

        // Only an exact "Password"/"TicketData" key is masked, not substrings.
        Assert.Contains("my-dog", masked);
        Assert.Contains("keep", masked);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("{\"UserName\":\"bob\"}")]
    public void PassesThroughWhenNothingToMask(string json)
    {
        Assert.Equal(json, LogRedaction.MaskSensitiveJsonFields(json));
    }
}