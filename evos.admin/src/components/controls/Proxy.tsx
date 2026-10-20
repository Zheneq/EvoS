import React, {useState} from "react";
import {
    Alert,
    Box,
    Button,
    Card,
    LinearProgress,
    Snackbar,
    Typography
} from "@mui/material";
import {reloadProxyConfig} from "../../lib/Evos";
import {useAuthHeader} from "react-auth-kit";
import {processError} from "../../lib/Error";
import {useNavigate} from "react-router-dom";
import AltRouteRoundedIcon from "@mui/icons-material/AltRouteRounded";
import SyncRoundedIcon from "@mui/icons-material/SyncRounded";

export default function Proxy() {
    const [msg, setMsg] = useState<string>();
    const [severity, setSeverity] = useState<"success" | "info" | "warning" | "error">("info");
    const [processing, setProcessing] = useState<boolean>(false);
    const authHeader = useAuthHeader();
    const navigate = useNavigate();

    const handleClick = () => {
        setProcessing(true);
        const abort = new AbortController();
        reloadProxyConfig(abort, authHeader())
            .then(() => {
                setSeverity("success");
                setMsg("Proxy configuration reloaded successfully");
            })
            .catch(e => {
                setSeverity("error");
                if (e.response?.status === 404) {
                    setMsg("Proxy reload endpoint not found (404)");
                } else {
                    processError(e, err => setMsg(err.text || "Failed to reload proxy config"), navigate);
                }
            })
            .finally(() => setProcessing(false));

        return () => abort.abort();
    };

    return (
        <Card variant="outlined" sx={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
            <Box sx={{ p: 2.5, pb: 1.5 }}>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25, mb: 0.5 }}>
                    <AltRouteRoundedIcon color="primary" />
                    <Typography variant="h6" sx={{ fontWeight: 600 }}>
                        Proxy Routing
                    </Typography>
                </Box>
                <Typography variant="body2" color="text.secondary">
                    Hot-reload gateway proxy routing definitions on the fly.
                </Typography>
            </Box>

            <Box sx={{ p: 2.5, pt: 0, flexGrow: 1, display: 'flex', flexDirection: 'column', justifyContent: 'space-between', gap: 2.5 }}>
                <Typography variant="body2" color="text.secondary">
                    Applies modified proxy endpoints and routing tables without dropping active WebSocket sessions or restarting master lobby services.
                </Typography>

                <Button
                    variant="outlined"
                    color="primary"
                    fullWidth
                    disabled={processing}
                    onClick={handleClick}
                    startIcon={<SyncRoundedIcon />}
                >
                    Reload Proxy Config
                </Button>
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