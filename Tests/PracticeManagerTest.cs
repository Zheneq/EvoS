using CentralServer.LobbyServer.Practice;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using Tests.Lib;
using Xunit.Abstractions;

namespace Tests;

public class PracticeManagerTest(ITestOutputHelper output) : EvosTest(output)
{
    // Use unique per-test AccountIds (> 15_000_000 to avoid collision with other test ranges)
    private static long UniqueId() => (Guid.NewGuid().GetHashCode() & 0x7FFFFFFF) + 15_000_000L;

    private static long MakeAccount(CharacterType character)
    {
        long accountId = UniqueId();
        DB.Get().AccountDao.CreateAccount(new PersistedAccountData
        {
            AccountId = accountId,
            Handle = $"Test#{accountId}",
            AccountComponent = new AccountComponent
            {
                LastCharacter = character,
            },
            CharacterData = new Dictionary<CharacterType, PersistedCharacterData>
            {
                { character, new PersistedCharacterData(character) }
            },
        });
        return accountId;
    }

    [Fact]
    public void StartPractice_WillFill_Rejected()
    {
        long accountId = MakeAccount(CharacterType.PendingWillFill);

        PracticeGame game = PracticeManager.StartPractice(accountId, out LocalizationPayload error, out bool allowRetry);

        Assert.Null(game);
        Assert.Equal("QueueDoesNotAllowWillFill", error.Term);
        Assert.Equal("Matchmaking", error.Context);
        Assert.True(allowRetry);
    }

    [Theory]
    [InlineData(CharacterType.TestFreelancer1)]
    [InlineData(CharacterType.TestFreelancer2)]
    [InlineData(CharacterType.PunchingDummy)]
    [InlineData(CharacterType.None)]
    [InlineData((CharacterType)999)]
    public void StartPractice_UnplayableCharacter_Rejected(CharacterType character)
    {
        long accountId = MakeAccount(character);

        PracticeGame game = PracticeManager.StartPractice(accountId, out LocalizationPayload error, out bool allowRetry);

        Assert.Null(game);
        Assert.Equal("CannotReady", error.Term);
        Assert.Equal("Global", error.Context);
        Assert.True(allowRetry);
    }

    [Fact]
    public void StartPractice_PlayableCharacter_PassesCharacterCheck()
    {
        long accountId = MakeAccount(CharacterType.Archer);

        // No game servers are registered in tests, so a playable character gets as far as picking a server
        PracticeGame game = PracticeManager.StartPractice(accountId, out LocalizationPayload error, out _);

        Assert.Null(game);
        Assert.Equal("FailedStartGameServer", error.Term);
    }
}
