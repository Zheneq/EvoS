import React, {useEffect, useState} from 'react';
import {asDate, ban, clearQueuePenalty, formatDate, getPlayer, mute, PlayerDetails, setVip} from "../../lib/Evos";
import {EvosError, processError} from "../../lib/Error";
import {useAuthHeader} from "react-auth-kit";
import {useNavigate, useParams} from "react-router-dom";
import Player from "../atlas/Player";
import {Button, LinearProgress, Paper} from "@mui/material";
import ErrorDialog from "../generic/ErrorDialog";
import MuteBanPlayer from "../controls/MuteBanPlayer";
import {EvosCard, StackWrapper} from "../generic/BasicComponents";
import AdminMessages from "../controls/AdminMessages";
import TempPassword from "../controls/TempPassword";
import SendWhisper from "../controls/SendWhisper";
import BaseDialog from "../generic/BaseDialog";


export default function ProfilePage() {
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<EvosError>();
    const [playerDetails, setPlayerDetails] = useState<PlayerDetails>();
    const [lastAction, setLastAction] = useState<Date>();
    const [confirmVipOpen, setConfirmVipOpen] = useState(false);
    const [vipProcessing, setVipProcessing] = useState(false);
    const [confirmClearQueueOpen, setConfirmClearQueueOpen] = useState(false);
    const [queueProcessing, setQueueProcessing] = useState(false);

    const {accountId} = useParams();
    const accountIdNumber = accountId && parseInt(accountId);

    const authHeader = useAuthHeader()();
    const navigate = useNavigate();

    const handleCommit = () => {
        setLastAction(new Date());
    }

    useEffect(() => {
        if (!accountIdNumber) return;
        setLoading(true);
        const abort = new AbortController();
        getPlayer(abort, authHeader, accountIdNumber)
            .then((resp) => {
                setPlayerDetails(resp.data);
                document.title = `Account ${resp.data.player.handle}`;
                setLoading(false);
            })
            .catch((error) => processError(error, setError, navigate));

        return () => abort.abort();
    }, [accountIdNumber, authHeader, navigate, setPlayerDetails, lastAction]);

    const handle = `${playerDetails?.player.handle ?? "Nobody"}`;
    const isVip = playerDetails?.isVip;
    const queueBlockedUntil = playerDetails?.queueBlockedUntil;
    const queueDodgeCount = playerDetails?.queueDodgeCount ?? 0;

    const handleClearQueueConfirm = () => {
        if (!accountIdNumber) return;
        setQueueProcessing(true);
        setConfirmClearQueueOpen(false);
        const abort = new AbortController();
        clearQueuePenalty(abort, authHeader, accountIdNumber)
            .then(() => handleCommit())
            .catch(e => processError(e, setError, navigate))
            .finally(() => setQueueProcessing(false));
    };

    const handleVipConfirm = () => {
        if (!accountIdNumber) return;
        setVipProcessing(true);
        setConfirmVipOpen(false);
        const abort = new AbortController();
        setVip(abort, authHeader, accountIdNumber, !isVip)
            .then(() => handleCommit())
            .catch(e => processError(e, setError, navigate))
            .finally(() => setVipProcessing(false));
    };

    return (
        <Paper>
            {error && <ErrorDialog error={error} onDismiss={() => setError(undefined)} />}
            <BaseDialog
                title={confirmVipOpen ? `${isVip ? 'Revoke' : 'Grant'} VIP for ${handle}?` : undefined}
                onDismiss={() => setConfirmVipOpen(false)}
                onAccept={handleVipConfirm}
                acceptText={isVip ? 'Revoke VIP' : 'Grant VIP'}
            />
            <BaseDialog
                title={confirmClearQueueOpen ? `Clear queue penalty for ${handle}?` : undefined}
                onDismiss={() => setConfirmClearQueueOpen(false)}
                onAccept={handleClearQueueConfirm}
                acceptText={'Clear queue penalty'}
            />
            <StackWrapper>
                <EvosCard variant="outlined"><Player info={playerDetails?.player} /></EvosCard>
                {loading && <LinearProgress />}
                <EvosCard variant="outlined">
                    <Button onClick={() => navigate(`/account/${accountIdNumber}/matches`)}>Match History</Button>
                    <Button onClick={() => navigate(`/account/${accountIdNumber}/chat`)}>Chat History</Button>
                    <Button onClick={() => navigate(`/account/${accountIdNumber}/feedback`)}>Feedback History</Button>
                </EvosCard>
                <EvosCard variant="outlined">
                    <Button
                        disabled={loading || vipProcessing}
                        onClick={() => setConfirmVipOpen(true)}
                    >
                        {isVip ? 'Revoke VIP' : 'Grant VIP'}
                    </Button>
                </EvosCard>
                <MuteBanPlayer
                    disabled={loading}
                    deadline={asDate(playerDetails?.mutedUntil)}
                    accountId={playerDetails?.player.accountId ?? 0}
                    action={mute}
                    handle={handle}
                    actionText={"mute"}
                    doneText={"muted"}
                    onCommit={handleCommit}
                />
                <MuteBanPlayer
                    disabled={loading}
                    deadline={asDate(playerDetails?.bannedUntil)}
                    accountId={playerDetails?.player.accountId ?? 0}
                    action={ban}
                    handle={handle}
                    actionText={"ban"}
                    doneText={"banned"}
                    onCommit={handleCommit}
                />
                <EvosCard variant="outlined">
                    <div>
                        {queueBlockedUntil
                            ? `Queue blocked until ${formatDate(queueBlockedUntil)} (offenses: ${queueDodgeCount})`
                            : `No active queue penalty${queueDodgeCount > 0 ? ` (offenses: ${queueDodgeCount})` : ''}`}
                    </div>
                    <Button
                        disabled={loading || queueProcessing || (!queueBlockedUntil && queueDodgeCount === 0)}
                        onClick={() => setConfirmClearQueueOpen(true)}
                    >
                        Clear queue penalty
                    </Button>
                </EvosCard>
                <AdminMessages accountId={playerDetails?.player.accountId ?? 0} />
                <SendWhisper accountId={playerDetails?.player.accountId ?? 0} handle={handle} />
                <EvosCard variant="outlined">
                    <TempPassword accountId={playerDetails?.player.accountId ?? 0}/>
                </EvosCard>
            </StackWrapper>
        </Paper>
    );
}
