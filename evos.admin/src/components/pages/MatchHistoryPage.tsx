import React, {useState} from 'react';
import {EvosError} from "../../lib/Error";
import {useParams} from "react-router-dom";
import {Paper} from "@mui/material";
import ErrorDialog from "../generic/ErrorDialog";
import {MatchHistory} from "../controls/MatchHistory";


export default function MatchHistoryPage() {
    const [error, setError] = useState<EvosError>();
    const {accountId} = useParams();

    return (
        <Paper>
            {error && <ErrorDialog error={error} onDismiss={() => setError(undefined)} />}
            <MatchHistory accountId={accountId ? parseInt(accountId) : 0}/>
        </Paper>
    );
}
