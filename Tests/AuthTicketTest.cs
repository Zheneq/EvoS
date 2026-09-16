using EvoS.Framework.Auth;

namespace Tests;

// Ticket XML is attacker-controllable and must be parsed without DTD/external-entity processing
// (no XXE / billion-laughs) and with a size cap.
public class AuthTicketTest
{
    private const string ValidTicket =
        "<authTicket><account>" +
        "<email>user@example.com</email>" +
        "<accountId>123</accountId>" +
        "<glyphTag>User#1</glyphTag>" +
        "<accountCurrency>USD</accountCurrency>" +
        "<accountStatus>ACTIVE</accountStatus>" +
        "</account></authTicket>";

    [Fact]
    public void Parse_ValidTicket_ReadsAccountFields()
    {
        AuthTicket ticket = AuthTicket.Parse(ValidTicket);

        Assert.Equal("user@example.com", ticket.UserName);
        Assert.Equal(123L, ticket.AccountId);
        Assert.Equal("User#1", ticket.Handle);
    }

    [Fact]
    public void Parse_WithDoctypeDtd_IsRejected()
    {
        // A DOCTYPE declaring an external entity - classic XXE vector. DtdProcessing.Prohibit must reject it.
        string xxe =
            "<?xml version=\"1.0\"?>" +
            "<!DOCTYPE authTicket [ <!ENTITY xxe SYSTEM \"file:///etc/passwd\"> ]>" +
            "<authTicket><account><email>&xxe;</email><accountId>1</accountId>" +
            "<glyphTag>x#1</glyphTag><accountCurrency>USD</accountCurrency>" +
            "<accountStatus>ACTIVE</accountStatus></account></authTicket>";

        Assert.ThrowsAny<Exception>(() => AuthTicket.Parse(xxe));
        Assert.Null(AuthTicket.TryParse(xxe)); // TryParse must swallow the error and return null
    }

    [Fact]
    public void Parse_OversizedXml_IsRejected()
    {
        string huge = "<authTicket><account><email>" + new string('a', 512 * 1024) +
                      "</email></account></authTicket>";

        Assert.ThrowsAny<Exception>(() => AuthTicket.Parse(huge));
    }
}
