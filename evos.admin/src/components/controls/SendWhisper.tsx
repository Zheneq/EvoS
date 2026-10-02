import {
    Alert,
    Box,
    Button,
    Card,
    LinearProgress,
    Snackbar,
    Stack,
    TextField,
    Typography
} from "@mui/material";
import {sendWhisper} from "../../lib/Evos";
import React, {useRef, useState} from "react";
import {useAuthHeader} from "react-auth-kit";
import {useNavigate} from "react-router-dom";
import {EvosError, processError} from "../../lib/Error";
import ErrorDialog from "../generic/ErrorDialog";
import SendRoundedIcon from "@mui/icons-material/SendRounded";

interface SendWhisperProps {
    accountId: number;
    handle: string;
}

export default function SendWhisper({accountId, handle}: SendWhisperProps) {
    const [processing, setProcessing] = useState<boolean>(false);
    const [error, setError] = useState<EvosError>();
    const [successOpen, setSuccessOpen] = useState(false);
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
                setSuccessOpen(true);
            })
            .catch(e => {
                setProcessing(false);
                processError(e, setError, navigate);
            });

        return () => abort.abort();
    };

    return (
        <Card variant="outlined">
            {error && <ErrorDialog error={error} onDismiss={() => setError(undefined)} />}
            <Snackbar
                open={successOpen}
                autoHideDuration={4000}
                onClose={() => setSuccessOpen(false)}
                anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
            >
                <Alert severity="success" onClose={() => setSuccessOpen(false)}>
                    Whisper sent to {handle}!
                </Alert>
            </Snackbar>

            <Stack spacing={2} sx={{ width: '100%' }}>
                <Box>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                        <SendRoundedIcon color="primary" />
                        <Typography variant="h6" sx={{ fontWeight: 600 }}>
                            Send In-Game Whisper
                        </Typography>
                    </Box>
                    <Typography variant="body2" color="text.secondary">
                        Send a direct administrative message to {handle} while they are in-game.
                    </Typography>
                </Box>

                <Box component="form" ref={formRef} onSubmit={handleSubmit} noValidate sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                    <TextField
                        size="small"
                        required
                        fullWidth
                        id="whisper"
                        label={`Message for ${handle}`}
                        name="whisper"
                        multiline
                        rows={3}
                        placeholder="Type whisper message..."
                        disabled={!accountId || processing}
                    />
                    <Button
                        disabled={!accountId || processing}
                        type="submit"
                        fullWidth
                        variant="contained"
                        color="primary"
                        startIcon={<SendRoundedIcon />}
                    >
                        Send Whisper
                    </Button>
                    {processing && <LinearProgress sx={{ borderRadius: 1 }} />}
                </Box>
            </Stack>
        </Card>
    );
}

