import React, {useEffect, useState} from 'react';
import {asDate, ban, getPlayer, mute, PlayerDetails} from "../../lib/Evos";
import {EvosError, processError} from "../../lib/Error";
import {useAuthHeader} from "react-auth-kit";
import {useNavigate, useParams} from "react-router-dom";
import Player from "../atlas/Player";
import {LinearProgress, Paper} from "@mui/material";
import ErrorDialog from "../generic/ErrorDialog";
import MuteBanPlayer from "../controls/MuteBanPlayer";
import {EvosCard, StackWrapper} from "../generic/BasicComponents";
import AdminMessages from "../controls/AdminMessages";
import TempPassword from "../controls/TempPassword";


export default function ProfilePage() {
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<EvosError>();
    const [playerDetails, setPlayerDetails] = useState<PlayerDetails>();
    const [lastAction, setLastAction] = useState<Date>();

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

    return (
        <Paper>
            {error && <ErrorDialog error={error} onDismiss={() => setError(undefined)} />}
            <StackWrapper>
                <EvosCard variant="outlined"><Player info={playerDetails?.player} /></EvosCard>
                {loading && <LinearProgress />}
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
                <AdminMessages accountId={playerDetails?.player.accountId ?? 0} />
                <EvosCard variant="outlined">
                    <TempPassword accountId={playerDetails?.player.accountId ?? 0}/>
                </EvosCard>
            </StackWrapper>
        </Paper>
    );
}
