import React, {useState} from "react";
import {
    Alert,
    Box,
    Button,
    Card,
    Dialog,
    DialogActions,
    DialogContent,
    DialogContentText,
    DialogTitle,
    Grid,
    LinearProgress,
    Snackbar,
    Typography
} from "@mui/material";
import {PendingShutdownType, scheduleShutdown} from "../../lib/Evos";
import {useAuthHeader} from "react-auth-kit";
import {processError} from "../../lib/Error";
import {useNavigate} from "react-router-dom";
import PowerSettingsNewRoundedIcon from "@mui/icons-material/PowerSettingsNewRounded";
import HourglassTopRoundedIcon from "@mui/icons-material/HourglassTopRounded";
import PeopleOutlineRoundedIcon from "@mui/icons-material/PeopleOutlineRounded";
import CancelRoundedIcon from "@mui/icons-material/CancelRounded";
import WarningAmberRoundedIcon from "@mui/icons-material/WarningAmberRounded";

export default function Shutdown() {
    const [msg, setMsg] = useState<string>();
    const [severity, setSeverity] = useState<"success" | "info" | "warning" | "error">("info");
    const [processing, setProcessing] = useState<boolean>(false);
    const [confirmNowOpen, setConfirmNowOpen] = useState<boolean>(false);
    const authHeader = useAuthHeader();
    const navigate = useNavigate();

    const executeShutdown = (type: PendingShutdownType) => {
        const text =
            type === PendingShutdownType.Now
                ? "Immediate server shutdown initiated"
                : type === PendingShutdownType.WaitForGamesToEnd
                    ? "Server set to shut down after active matches finish"
                    : type === PendingShutdownType.WaitForPlayersToLeave
                        ? "Server set to shut down once all players leave"
                        : "Scheduled server shutdown cancelled";

        setProcessing(true);
        const abort = new AbortController();
        scheduleShutdown(abort, authHeader(), type)
            .then(() => {
                setSeverity(type === PendingShutdownType.None ? "info" : "warning");
                setMsg(text);
            })
            .catch(e => {
                setSeverity("error");
                processError(e, err => setMsg(err.text || "Failed to schedule shutdown"), navigate);
            })
            .finally(() => setProcessing(false));

        return () => abort.abort();
    };

    return (
        <Card variant="outlined" sx={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
            <Box sx={{ p: 2.5, pb: 1.5 }}>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25, mb: 0.5 }}>
                    <PowerSettingsNewRoundedIcon color="error" />
                    <Typography variant="h6" sx={{ fontWeight: 600 }}>
                        Server Lifecycle & Shutdown
                    </Typography>
                </Box>
                <Typography variant="body2" color="text.secondary">
                    Schedule orderly maintenance shutdowns or execute immediate service termination.
                </Typography>
            </Box>

            <Box sx={{ p: 2.5, pt: 0, flexGrow: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
                <Typography variant="body2" color="text.secondary">
                    Orderly modes allow players to conclude their games without interruption. Scheduled modes can be cancelled at any time before server exit.
                </Typography>

                <Grid container spacing={1.5} sx={{ mt: 'auto' }}>
                    <Grid size={{ xs: 12, sm: 6 }}>
                        <Button
                            variant="outlined"
                            color="warning"
                            fullWidth
                            disabled={processing}
                            onClick={() => executeShutdown(PendingShutdownType.WaitForGamesToEnd)}
                            startIcon={<HourglassTopRoundedIcon />}
                            sx={{ height: '100%', py: 1 }}
                        >
                            Wait for Games
                        </Button>
                    </Grid>
                    <Grid size={{ xs: 12, sm: 6 }}>
                        <Button
                            variant="outlined"
                            color="warning"
                            fullWidth
                            disabled={processing}
                            onClick={() => executeShutdown(PendingShutdownType.WaitForPlayersToLeave)}
                            startIcon={<PeopleOutlineRoundedIcon />}
                            sx={{ height: '100%', py: 1 }}
                        >
                            Wait for Players
                        </Button>
                    </Grid>
                    <Grid size={{ xs: 12, sm: 6 }}>
                        <Button
                            variant="outlined"
                            color="inherit"
                            fullWidth
                            disabled={processing}
                            onClick={() => executeShutdown(PendingShutdownType.None)}
                            startIcon={<CancelRoundedIcon />}
                            sx={{ height: '100%', py: 1 }}
                        >
                            Cancel Shutdown
                        </Button>
                    </Grid>
                    <Grid size={{ xs: 12, sm: 6 }}>
                        <Button
                            variant="contained"
                            color="error"
                            fullWidth
                            disabled={processing}
                            onClick={() => setConfirmNowOpen(true)}
                            startIcon={<PowerSettingsNewRoundedIcon />}
                            sx={{ height: '100%', py: 1 }}
                        >
                            Shutdown Now
                        </Button>
                    </Grid>
                </Grid>
            </Box>

            {processing && <LinearProgress sx={{ borderRadius: 1 }} />}

            {/* Confirmation Dialog for Immediate Shutdown */}
            <Dialog
                open={confirmNowOpen}
                onClose={() => setConfirmNowOpen(false)}
                maxWidth="xs"
                fullWidth
            >
                <DialogTitle sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                    <WarningAmberRoundedIcon color="error" />
                    Immediate Server Shutdown
                </DialogTitle>
                <DialogContent>
                    <DialogContentText>
                        Are you sure you want to shut down the master server <strong>immediately</strong>?
                        All active matches will be terminated and all connected players will be disconnected.
                    </DialogContentText>
                </DialogContent>
                <DialogActions sx={{ px: 3, pb: 2 }}>
                    <Button onClick={() => setConfirmNowOpen(false)}>
                        Cancel
                    </Button>
                    <Button
                        variant="contained"
                        color="error"
                        onClick={() => {
                            setConfirmNowOpen(false);
                            executeShutdown(PendingShutdownType.Now);
                        }}
                    >
                        Confirm Shutdown
                    </Button>
                </DialogActions>
            </Dialog>

            {/* Notification Feedback */}
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