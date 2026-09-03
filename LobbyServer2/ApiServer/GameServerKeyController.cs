using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using CentralServer.BridgeServer;
using EvoS.Framework.DataAccess;
using EvoS.Framework.DataAccess.Daos;
using log4net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CentralServer.ApiServer;

public static class GameServerKeyController
{
    private static readonly ILog log = LogManager.GetLogger(typeof(GameServerKeyController));

    public class SetGameServerKeyStatusRequest
    {
        // true = approve, false = decline (pending) or revoke (approved).
        public bool Approve { get; set; }
        // Optional friendly name, applied when approving.
        public string Name { get; set; }
    }

    public class GameServerKeyResponse
    {
        public string Fingerprint { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
        public DateTime FirstSeenAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string ApprovedByHandle { get; set; }
        public DateTime? LastConnectedAt { get; set; }
        public string LastAddress { get; set; }
        public string LastBuildVersion { get; set; }
    }

    public class GameServerKeysResponse
    {
        public List<GameServerKeyResponse> Keys { get; set; }
    }

    public static IResult GetKeys(ClaimsPrincipal user)
    {
        if (!AdminController.ValidateAdmin(user, out IResult error, out _, out _))
        {
            return error;
        }

        List<GameServerKeyResponse> keys = GameServerKeyManager.GetAll().Select(ToResponse).ToList();
        return Results.Ok(new GameServerKeysResponse { Keys = keys });
    }

    public static IResult SetKeyStatus(string fingerprint, [FromBody] SetGameServerKeyStatusRequest data, ClaimsPrincipal user)
    {
        if (!AdminController.ValidateAdmin(user, out IResult error, out long adminAccountId, out string adminHandle))
        {
            return error;
        }

        bool approve = data?.Approve == true;
        bool ok = approve
            ? GameServerKeyManager.Approve(fingerprint, adminAccountId, data.Name)
            : GameServerKeyManager.Reject(fingerprint, adminAccountId);
        if (!ok)
        {
            return Results.NotFound();
        }

        log.Info($"Game server key {fingerprint} {(approve ? "approved" : "declined/revoked")} by {adminHandle} ({adminAccountId})");
        return Results.Ok();
    }

    private static GameServerKeyResponse ToResponse(GameServerKeyDao.GameServerKey k)
    {
        string handle = k.ApprovedByAccountId is { } id ? DB.Get().AccountDao.GetAccount(id)?.Handle : null;
        return new GameServerKeyResponse
        {
            Fingerprint = k.Fingerprint,
            Name = k.Name,
            Status = k.Status.ToString(),
            FirstSeenAt = k.FirstSeenAt,
            ApprovedAt = k.ApprovedAt,
            ApprovedByHandle = handle,
            LastConnectedAt = k.LastConnectedAt,
            LastAddress = k.LastAddress,
            LastBuildVersion = k.LastBuildVersion,
        };
    }
}
