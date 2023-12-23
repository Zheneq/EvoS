using System.Collections.Generic;
using EvoS.Framework.Network.Static;

namespace EvoS.Framework.DataAccess;

public delegate PersistedAccountData IAccountProvider(long accountId);
public delegate List<PersistedCharacterMatchData> IMatchHistoryProvider(long accountId);