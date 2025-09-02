import React, {useEffect, useState} from 'react';
import {EvosError, processError} from "../../lib/Error";
import {useNavigate, useParams} from "react-router-dom";
import {Paper} from "@mui/material";
import ErrorDialog from "../generic/ErrorDialog";
import Player from "../atlas/Player";
import {getPlayer, PlayerData} from "../../lib/Evos";
import {useAuthHeader} from "react-auth-kit";
import {ReportHistory} from "../controls/ReportHistory";


export default function ReportHistoryPage() {
    const {accountId} = useParams();
    
    const [player, setPlayer] = useState<PlayerData>();
    
    const [error, setError] = useState<EvosError>();
    const authHeader = useAuthHeader()();
    const navigate = useNavigate();

    useEffect(() => {
        const accId = accountId ? parseInt(accountId) : 0;
        
        if (accId === 0) {
            return;
        }

        const abort = new AbortController();

        getPlayer(abort, authHeader, accId)
            .then((resp) => setPlayer(resp.data.player))
            .catch((error) => processError(error, setError, navigate))

        return () => abort.abort();
    }, [accountId, authHeader, navigate]);

    return (
        <Paper>
            {error && <ErrorDialog error={error} onDismiss={() => setError(undefined)} />}
            <Player info={player} />
            <ReportHistory accountId={accountId ? parseInt(accountId) : 0} setError={setError}/>
        </Paper>
    );
}
