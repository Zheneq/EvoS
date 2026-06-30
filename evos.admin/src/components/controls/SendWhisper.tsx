import {Box, Button, LinearProgress, TextField} from "@mui/material";
import {sendWhisper} from "../../lib/Evos";
import React, {useRef, useState} from "react";
import {useAuthHeader} from "react-auth-kit";
import {useNavigate} from "react-router-dom";
import {EvosError, processError} from "../../lib/Error";
import {EvosCard} from "../generic/BasicComponents";
import ErrorDialog from "../generic/ErrorDialog";


interface SendWhisperProps {
    accountId: number;
    handle: string;
}

export default function SendWhisper({accountId, handle}: SendWhisperProps) {
    const [processing, setProcessing] = useState<boolean>(false);
    const [error, setError] = useState<EvosError>();
    const formRef = useRef<HTMLFormElement>(null);

    const authHeader = useAuthHeader()();
    const navigate = useNavigate();

    const handleSubmit = (event: React.FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        const data = new FormData(event.currentTarget);
        const message = data.get('whisper') as string;

        if (!message) {
            return;
        }

        setProcessing(true);
        const abort = new AbortController();
        sendWhisper(abort, authHeader, accountId, "Admin", message)
            .then(() => {
                setProcessing(false);
                formRef.current?.reset();
            })
            .catch(e => {
                setProcessing(false);
                processError(e, setError, navigate);
            });

        return () => abort.abort();
    };

    return <EvosCard variant="outlined">
        {error && <ErrorDialog error={error} onDismiss={() => setError(undefined)} />}
        <Box component="form" ref={formRef} onSubmit={handleSubmit} noValidate style={{ padding: 4 }}>
            <TextField
                margin="normal"
                required
                fullWidth
                id="whisper"
                label={`Whisper to ${handle}`}
                name="whisper"
                multiline
                disabled={!accountId || processing}
            />
            <Button
                disabled={!accountId || processing}
                type="submit"
                fullWidth
                variant="contained"
                sx={{ mt: 3, mb: 2 }}
            >
                Send whisper
            </Button>
            {processing && <LinearProgress />}
        </Box>
    </EvosCard>;
}
