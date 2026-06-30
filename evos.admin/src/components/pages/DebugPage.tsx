import React, {useState} from 'react';
import {startMapPickBan} from "../../lib/Evos";
import {EvosError, processError} from "../../lib/Error";
import {useAuthHeader} from "react-auth-kit";
import {useNavigate} from "react-router-dom";
import {Button, Paper, TextField, Typography} from "@mui/material";
import ErrorDialog from "../generic/ErrorDialog";
import {EvosCard, StackWrapper} from "../generic/BasicComponents";


export default function DebugPage() {
    const [error, setError] = useState<EvosError>();
    const [captainAId, setCaptainAId] = useState('');
    const [captainBId, setCaptainBId] = useState('');

    const authHeader = useAuthHeader()();
    const navigate = useNavigate();

    const handleMapPickBan = (pickCount: number) => {
        const captainA = parseInt(captainAId);
        const captainB = parseInt(captainBId);
        if (isNaN(captainA) || isNaN(captainB)) return;
        const abort = new AbortController();
        startMapPickBan(abort, authHeader, captainA, captainB, pickCount)
            .catch((err) => processError(err, setError, navigate));
    };

    const bothCaptainsSet = captainAId && captainBId;

    return (
        <Paper>
            {error && <ErrorDialog error={error} onDismiss={() => setError(undefined)} />}
            <StackWrapper>
                <EvosCard variant="outlined">
                    <Typography variant="subtitle2">Map Pick/Ban</Typography>
                    <TextField
                        label="Captain A account ID"
                        size="small"
                        value={captainAId}
                        onChange={(e) => setCaptainAId(e.target.value)}
                        type="number"
                    />
                    <TextField
                        label="Captain B account ID"
                        size="small"
                        value={captainBId}
                        onChange={(e) => setCaptainBId(e.target.value)}
                        type="number"
                    />
                    <Button disabled={!bothCaptainsSet} onClick={() => handleMapPickBan(1)}>Pick 1 map</Button>
                    <Button disabled={!bothCaptainsSet} onClick={() => handleMapPickBan(3)}>Pick 3 maps</Button>
                    <Button disabled={!bothCaptainsSet} onClick={() => handleMapPickBan(5)}>Pick 5 maps</Button>
                </EvosCard>
            </StackWrapper>
        </Paper>
    );
}