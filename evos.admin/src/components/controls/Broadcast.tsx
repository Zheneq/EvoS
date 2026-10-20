import React, {useState} from "react";
import {
    Alert,
    Box,
    Button,
    Card,
    LinearProgress,
    Snackbar,
    TextField,
    Typography
} from "@mui/material";
import {broadcast} from "../../lib/Evos";
import {useAuthHeader} from "react-auth-kit";
import {useNavigate} from "react-router-dom";
import {processError} from "../../lib/Error";
import CampaignRoundedIcon from "@mui/icons-material/CampaignRounded";
import SendRoundedIcon from "@mui/icons-material/SendRounded";

export default function Broadcast() {
    const [message, setMessage] = useState<string>("");
    const [msg, setMsg] = useState<string>();
    const [severity, setSeverity] = useState<"success" | "info" | "warning" | "error">("info");
    const [processing, setProcessing] = useState<boolean>(false);
    const authHeader = useAuthHeader();
    const navigate = useNavigate();

    const handleSubmit = (event: React.FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        const trimmed = message.trim();
        if (!trimmed) return;

        setProcessing(true);
        const abort = new AbortController();
        broadcast(abort, authHeader(), trimmed)
            .then(() => {
                setSeverity("success");
                setMsg("Broadcast message dispatched to all connected players");
                setMessage("");
            })
            .catch(e => {
                setSeverity("error");
                processError(e, err => setMsg(err.text || "Failed to send broadcast"), navigate);
            })
            .finally(() => setProcessing(false));

        return () => abort.abort();
    };

    return (
        <Card variant="outlined" sx={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
            <Box sx={{ p: 2.5, pb: 1.5 }}>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25, mb: 0.5 }}>
                    <CampaignRoundedIcon color="primary" />
                    <Typography variant="h6" sx={{ fontWeight: 600 }}>
                        Global In-Game Broadcast
                    </Typography>
                </Box>
                <Typography variant="body2" color="text.secondary">
                    Transmit an instant system notification to all currently online players.
                </Typography>
            </Box>

            <Box
                component="form"
                onSubmit={handleSubmit}
                noValidate
                sx={{ p: 2.5, pt: 0, flexGrow: 1, display: 'flex', flexDirection: 'column', gap: 2 }}
            >
                <TextField
                    fullWidth
                    required
                    multiline
                    rows={2}
                    id="broadcast-message"
                    label="Announcement Message"
                    placeholder="Enter message to broadcast across all game sessions..."
                    value={message}
                    onChange={(e) => setMessage(e.target.value)}
                    disabled={processing}
                    helperText="This message will immediately appear on the screens of all connected players."
                />

                <Box sx={{ display: 'flex', justifyContent: 'flex-end', mt: 'auto' }}>
                    <Button
                        type="submit"
                        variant="contained"
                        color="primary"
                        disabled={processing || !message.trim()}
                        startIcon={<SendRoundedIcon />}
                    >
                        Send Broadcast
                    </Button>
                </Box>
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