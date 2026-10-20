import React, {useState} from "react";
import {
    Alert,
    Box,
    Button,
    Card,
    LinearProgress,
    Snackbar,
    Stack,
    Typography
} from "@mui/material";
import {pauseQueue} from "../../lib/Evos";
import {useAuthHeader} from "react-auth-kit";
import {processError} from "../../lib/Error";
import {useNavigate} from "react-router-dom";
import SportsEsportsRoundedIcon from "@mui/icons-material/SportsEsportsRounded";
import PauseCircleOutlineRoundedIcon from "@mui/icons-material/PauseCircleOutlineRounded";
import PlayArrowRoundedIcon from "@mui/icons-material/PlayArrowRounded";

export default function PauseQueue() {
    const [msg, setMsg] = useState<string>();
    const [severity, setSeverity] = useState<"success" | "info" | "warning" | "error">("info");
    const [processing, setProcessing] = useState<boolean>(false);
    const authHeader = useAuthHeader();
    const navigate = useNavigate();

    const handleClick = (paused: boolean) => {
        const text = paused ? "Matchmaking queue paused successfully" : "Matchmaking queue resumed";
        setProcessing(true);
        const abort = new AbortController();
        pauseQueue(abort, authHeader(), paused)
            .then(() => {
                setSeverity(paused ? "warning" : "success");
                setMsg(text);
            })
            .catch(e => {
                setSeverity("error");
                processError(e, err => setMsg(err.text || "Failed to change queue status"), navigate);
            })
            .finally(() => setProcessing(false));

        return () => abort.abort();
    };

    return (
        <Card variant="outlined" sx={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
            <Box sx={{ p: 2.5, pb: 1.5 }}>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25, mb: 0.5 }}>
                    <SportsEsportsRoundedIcon color="primary" />
                    <Typography variant="h6" sx={{ fontWeight: 600 }}>
                        Matchmaking Queue
                    </Typography>
                </Box>
                <Typography variant="body2" color="text.secondary">
                    Control player queue entry across all casual and competitive game modes.
                </Typography>
            </Box>

            <Box sx={{ p: 2.5, pt: 0, flexGrow: 1, display: 'flex', flexDirection: 'column', justifyContent: 'space-between', gap: 2.5 }}>
                <Typography variant="body2" color="text.secondary">
                    Pausing matchmaking stops players from queuing into matches. Ongoing games are unaffected and can conclude normally.
                </Typography>

                <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
                    <Button
                        variant="outlined"
                        color="warning"
                        fullWidth
                        disabled={processing}
                        onClick={() => handleClick(true)}
                        startIcon={<PauseCircleOutlineRoundedIcon />}
                    >
                        Pause Queue
                    </Button>
                    <Button
                        variant="contained"
                        color="success"
                        fullWidth
                        disabled={processing}
                        onClick={() => handleClick(false)}
                        startIcon={<PlayArrowRoundedIcon />}
                    >
                        Resume Queue
                    </Button>
                </Stack>
            </Box>

            {processing && <LinearProgress sx={{ borderRadius: 1 }} />}

            <Snackbar
                open={!!msg}
                autoHideDuration={5000}
                onClose={() => setMsg(undefined)}
                anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
            >
                <Alert
                    onClose={() => setMsg(undefined)}
                    severity={severity}
                    variant="filled"
                    sx={{ width: '100%', boxShadow: 4 }}
                >
                    {msg}
                </Alert>
            </Snackbar>
        </Card>
    );
}