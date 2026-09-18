using System.Collections.Generic;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.TrustWar;
using EvoS.DirectoryServer.Inventory;
using EvoS.Framework;
using EvoS.Framework.DataAccess;
using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using log4net;

namespace CentralServer.LobbyServer.Account;

public class AccountModule : ILobbyModule
{
    private static readonly ILog log = LogManager.GetLogger(typeof(AccountModule));
    private readonly IClientConnection _conn;

    public AccountModule(IClientConnection conn)
    {
        _conn = conn;
    }

    public void Register(IHandlerRegistry registry)
    {
        registry.Register<OptionsNotification>(HandleOptionsNotification);
        registry.Register<EvosOptionsNotification>(HandleEvosOptionsNotification);
        registry.Register<EvosOptionsNotificationLegacy>(HandleEvosOptionsNotificationLegacy);
        registry.Register<CustomKeyBindNotification>(HandleCustomKeyBindNotification);
        registry.Register<SetDevTagRequest>(HandleSetDevTagRequest);
        registry.Register<UpdateUIStateRequest>(HandleUpdateUIStateRequest);
        registry.Register<SetRegionRequest>(HandleSetRegionRequest);
        registry.Register<LoadingScreenToggleRequest>(HandleLoadingScreenToggleRequest);
        registry.Register<CheckRAFStatusRequest>(HandleCheckRAFStatusRequest);
        registry.Register<SendRAFReferralEmailsRequest>(HandleSendRAFReferralEmailsRequest);
        registry.Register<CheckAccountStatusRequest>(HandleCheckAccountStatusRequest);
        registry.Register<PlayerMatchDataRequest>(HandlePlayerMatchDataRequest);
        registry.Register<SelectBannerRequest>(HandleSelectBannerRequest);
        registry.Register<SelectTitleRequest>(HandleSelectTitleRequest);
        registry.Register<SelectRibbonRequest>(HandleSelectRibbonRequest);
    }

    private void HandleOptionsNotification(OptionsNotification notification)
    {
        HandleEvosOptionsNotification(EvosOptionsNotification.Of(notification));
    }

    private void HandleEvosOptionsNotification(EvosOptionsNotification notification)
    {
        DB.Get().UserMetadataDao.UpsertOptions(_conn.AccountId, notification);
    }

    private void HandleEvosOptionsNotificationLegacy(EvosOptionsNotificationLegacy notification)
    {
        DB.Get().UserMetadataDao.UpsertOptions(_conn.AccountId, notification.ToCurrent());
    }

    private void HandleCustomKeyBindNotification(CustomKeyBindNotification notification)
    {
        DB.Get().AccountDao.GetAccount(_conn.AccountId).AccountComponent.KeyCodeMapping = notification.CustomKeyBinds;
    }

    private void HandleSetDevTagRequest(SetDevTagRequest request)
    {
        PersistedAccountData account = DB.Get().AccountDao.GetAccount(_conn.AccountId);
        if (account == null)
        {
            return;
        }
        if (account.AccountComponent.IsDev())
        {
            account.AccountComponent.DisplayDevTag = request.active;
            _conn.Send(new SetDevTagResponse()
            {
                Success = true,
            });
        }
        else
        {
            _conn.Send(new SetDevTagResponse()
            {
                Success = false,
            });
        }
    }

    //Allows to get rid of the flashy New tag next to store for existing users
    private void HandleUpdateUIStateRequest(UpdateUIStateRequest request)
    {
        PersistedAccountData account = DB.Get().AccountDao.GetAccount(_conn.AccountId);
        log.Info($"Player {_conn.AccountId} requested UIState {request.UIState} {request.StateValue}");
        account.AccountComponent.UIStates[request.UIState] = request.StateValue;
        DB.Get().AccountDao.UpdateAccountComponent(account);
    }

    private void HandleSetRegionRequest(SetRegionRequest request)
    {
    }

    private void HandleLoadingScreenToggleRequest(LoadingScreenToggleRequest request)
    {
        PersistedAccountData account = DB.Get().AccountDao.GetAccount(_conn.AccountId);
        Dictionary<int, bool> bgs = account.AccountComponent.UnlockedLoadingScreenBackgroundIdsToActivatedState;
        if (bgs.ContainsKey(request.LoadingScreenId))
        {
            bgs[request.LoadingScreenId] = request.NewState;
            DB.Get().AccountDao.UpdateAccountComponent(account);
            _conn.Send(new LoadingScreenToggleResponse
            {
                LoadingScreenId = request.LoadingScreenId,
                CurrentState = request.NewState,
                Success = true,
                ResponseId = request.RequestId
            });
        }
        else
        {
            _conn.Send(new LoadingScreenToggleResponse
            {
                LoadingScreenId = request.LoadingScreenId,
                Success = false,
                ResponseId = request.RequestId
            });
        }
    }

    private void HandleCheckRAFStatusRequest(CheckRAFStatusRequest request)
    {
        CheckRAFStatusResponse response = new CheckRAFStatusResponse()
        {
            ReferralCode = "sampletext",
            ResponseId = request.RequestId
        };
        _conn.Send(response);
    }

    private void HandleSendRAFReferralEmailsRequest(SendRAFReferralEmailsRequest request)
    {
        _conn.Send(new SendRAFReferralEmailsResponse
        {
            Success = false,
            ResponseId = request.RequestId
        });
    }

    private void HandleCheckAccountStatusRequest(CheckAccountStatusRequest request)
    {
        CheckAccountStatusResponse response = new CheckAccountStatusResponse()
        {
            QuestOffers = new QuestOfferNotification() { OfferDailyQuest = false },
            ResponseId = request.RequestId
        };
        _conn.Send(response);

        if (LobbyConfiguration.IsTrustWarEnabled())
        {
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(_conn.AccountId);

            for (int i = 0; i < 3; i++)
            {
                _conn.Send(new PlayerFactionContributionChangeNotification()
                {
                    CompetitionId = 1,
                    FactionId = i,
                    AmountChanged = 0,
                    TotalXP = TrustWarManager.GetTotalXPByFactionID(account, i),
                    AccountID = account.AccountId,
                });
            }
        }
    }

    private void HandlePlayerMatchDataRequest(PlayerMatchDataRequest request)
    {
        PlayerMatchDataResponse response = new PlayerMatchDataResponse
        {
            MatchData = DB.Get().MatchHistoryDao.Find(_conn.AccountId),
            ResponseId = request.RequestId
        };
        _conn.Send(response);
    }

    private void HandleSelectBannerRequest(SelectBannerRequest request)
    {
        PersistedAccountData account = DB.Get().AccountDao.GetAccount(_conn.AccountId);

        //  Modify the correct type of banner
        if (InventoryManager.BannerIsForeground(request.BannerID))
        {
            account.AccountComponent.SelectedForegroundBannerID = request.BannerID;
        }
        else
        {
            account.AccountComponent.SelectedBackgroundBannerID = request.BannerID;
        }

        // Update the account
        DB.Get().AccountDao.UpdateAccountComponent(account);

        _conn.OnAccountVisualsUpdated();

        // Send response
        _conn.Send(new SelectBannerResponse()
        {
            BackgroundBannerID = account.AccountComponent.SelectedBackgroundBannerID,
            ForegroundBannerID = account.AccountComponent.SelectedForegroundBannerID,
            ResponseId = request.RequestId
        });
    }

    private void HandleSelectTitleRequest(SelectTitleRequest request)
    {
        PersistedAccountData account = DB.Get().AccountDao.GetAccount(_conn.AccountId);

        if (account.AccountComponent.UnlockedTitleIDs.Contains(request.TitleID) || request.TitleID == -1)
        {
            account.AccountComponent.SelectedTitleID = request.TitleID;
            DB.Get().AccountDao.UpdateAccountComponent(account);

            _conn.OnAccountVisualsUpdated();
        }

        _conn.Send(new SelectTitleResponse
        {
            CurrentTitleID = account.AccountComponent.SelectedTitleID,
            ResponseId = request.RequestId
        });
    }

    private void HandleSelectRibbonRequest(SelectRibbonRequest request)
    {
        PersistedAccountData account = DB.Get().AccountDao.GetAccount(_conn.AccountId);

        if (account == null || !(account.AccountComponent.UnlockedRibbonIDs.Contains(request.RibbonID) || request.RibbonID == -1))
        {
            _conn.Send(new SelectRibbonResponse()
            {
                Success = false,
                ResponseId = request.RequestId,
            });
            return;
        }

        account.AccountComponent.SelectedRibbonID = request.RibbonID;
        DB.Get().AccountDao.UpdateAccountComponent(account);

        _conn.OnAccountVisualsUpdated();

        _conn.Send(new SelectRibbonResponse()
        {
            CurrentRibbonID = request.RibbonID,
            Success = true,
            ResponseId = request.RequestId,
        });
    }
}
