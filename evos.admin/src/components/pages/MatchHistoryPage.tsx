import React, {useEffect, useState} from 'react';
import {
    asDate,
    getPlayer,
    getStatus,
    PlayerDetails,
    Status
} from "../../lib/Evos";
import {EvosError, processError} from "../../lib/Error";
import {useNavigate, useParams} from "react-router-dom";
import {
    Box,
    Button,
    Card,
    Chip,
    IconButton,
    LinearProgress,
    Stack,
    Tooltip,
    Typography
} from "@mui/material";
import ErrorDialog from "../generic/ErrorDialog";
import {MatchHistory} from "../controls/MatchHistory";
import {useAuthHeader} from "react-auth-kit";
import useInterval from "../../lib/useInterval";
import useHasFocus from "../../lib/useHasFocus";
import {useLocalStorage} from "../../lib/useLocalStorage";
import {Settings, SettingsKey} from "../../lib/Settings";
import PersonRoundedIcon from "@mui/icons-material/PersonRounded";
import SportsEsportsRoundedIcon from "@mui/icons-material/SportsEsportsRounded";
import ChatRoundedIcon from "@mui/icons-material/ChatRounded";
import FeedbackRoundedIcon from "@mui/icons-material/FeedbackRounded";
import StarRoundedIcon from "@mui/icons-material/StarRounded";
import VolumeOffRoundedIcon from "@mui/icons-material/VolumeOffRounded";
import GavelRoundedIcon from "@mui/icons-material/GavelRounded";
import BlockRoundedIcon from "@mui/icons-material/BlockRounded";
import RefreshRoundedIcon from "@mui/icons-material/RefreshRounded";

const UPDATE_PERIOD_MS = 20000;

export default function MatchHistoryPage() {
    const {accountId} = useParams();
    const accountIdNumber = accountId ? parseInt(accountId) : undefined;

    const [playerDetails, setPlayerDetails] = useState<PlayerDetails>();
    const [status, setStatus] = useState<Status>();
    const [lastAction, setLastAction] = useState<Date>();
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<EvosError>();

    const authHeader = useAuthHeader()();
    const navigate = useNavigate();

    const handleCommit = () => {
        setLastAction(new Date());
    };

    const [updateInBackground] = useLocalStorage(Settings.get(SettingsKey.updateInBackground)!);
    const hasFocus = useHasFocus();
    const updatePeriodMs = updateInBackground || hasFocus || !status ? UPDATE_PERIOD_MS : undefined;

    useInterval(() => {
        getStatus(authHeader)
            .then((resp) => {
                setStatus(resp.data);
            })
            .catch((error) => processError(error, setError, navigate));
    }, updatePeriodMs);

    useEffect(() => {
        if (!accountIdNumber) return;
        setLoading(true);
        const abort = new AbortController();

        getPlayer(abort, authHeader, accountIdNumber)
            .then((resp) => {
                setPlayerDetails(resp.data);
                document.title = `Matches: ${resp.data.player.handle}`;
                setLoading(false);
            })
            .catch((error) => processError(error, setError, navigate));

        getStatus(authHeader)
            .then((resp) => setStatus(resp.data))
            .catch((error) => processError(error, setError, navigate));

        return () => abort.abort();
    }, [accountIdNumber, authHeader, navigate, lastAction]);

    const handle = playerDetails?.player.handle ?? "Nobody";
    const isVip = playerDetails?.isVip ?? false;
    const queueBlockedUntil = playerDetails?.queueBlockedUntil;

    const mutedDate = asDate(playerDetails?.mutedUntil);
    const bannedDate = asDate(playerDetails?.bannedUntil);
    const blockedDate = asDate(queueBlockedUntil);

    const isMuted = !!mutedDate && mutedDate > new Date();
    const isBanned = !!bannedDate && bannedDate > new Date();
    const isQueueBlocked = !!blockedDate && blockedDate > new Date();

    const onlinePlayer = status?.players?.find(p => p.accountId === accountIdNumber);

    const isOnline = !!onlinePlayer;
    const currentStatus = onlinePlayer
        ? (onlinePlayer.status === "" ? "Online" : onlinePlayer.status)
        : (playerDetails?.player.status || "Offline");

    return (
        <Box sx={{ width: '100%', display: 'flex', flexDirection: 'column', gap: 3 }}>
            {error && <ErrorDialog error={error} onDismiss={() => setError(undefined)} />}

            {loading && <LinearProgress sx={{ borderRadius: 1 }} />}

            {/* Header with Title, Badges, and Quick Actions */}
            <Box
                sx={{
                    display: 'flex',
                    flexDirection: { xs: 'column', md: 'row' },
                    justifyContent: 'space-between',
                    alignItems: { xs: 'flex-start', md: 'center' },
                    gap: 2,
                    pb: 2,
                    borderBottom: '1px solid',
                    borderColor: 'divider',
                }}
            >
                <Box>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, flexWrap: 'wrap', mb: 1 }}>
                        <Typography variant="h4" component="h1" sx={{ fontWeight: 700, letterSpacing: '-0.5px' }}>
                            {handle}
                        </Typography>
                        {accountIdNumber && (
                            <Chip
                                label={`ID: ${accountIdNumber}`}
                                size="small"
                                variant="outlined"
                                sx={{ fontWeight: 600 }}
                            />
                        )}
                        <Chip
                            label={isVip ? "VIP Member" : "Standard"}
                            color={isVip ? "warning" : "default"}
                            icon={isVip ? <StarRoundedIcon /> : undefined}
                            size="small"
                            variant={isVip ? "filled" : "outlined"}
                        />
                        <Chip
                            label={currentStatus}
                            color={isOnline ? (currentStatus === "Online" ? "success" : "info") : "default"}
                            size="small"
                            variant={isOnline ? "filled" : "outlined"}
                        />
                        {isQueueBlocked && (
                            <Chip
                                label="Queue Blocked"
                                color="error"
                                icon={<BlockRoundedIcon />}
                                size="small"
                            />
                        )}
                        {isMuted && (
                            <Chip
                                label="Muted"
                                color="warning"
                                icon={<VolumeOffRoundedIcon />}
                                size="small"
                            />
                        )}
                        {isBanned && (
                            <Chip
                                label="Banned"
                                color="error"
                                icon={<GavelRoundedIcon />}
                                size="small"
                            />
                        )}
                    </Box>
                    <Typography variant="body2" color="text.secondary">
                        Match history, statistics, and game records for {handle}.
                    </Typography>
                </Box>

                {/* Quick Actions */}
                <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap sx={{ alignItems: 'center' }}>
                    <Button
                        variant="outlined"
                        size="small"
                        startIcon={<PersonRoundedIcon />}
                        onClick={() => navigate(`/account/${accountIdNumber}`)}
                    >
                        Profile
                    </Button>
                    <Button
                        variant="contained"
                        size="small"
                        startIcon={<SportsEsportsRoundedIcon />}
                    >
                        Matches
                    </Button>
                    <Button
                        variant="outlined"
                        size="small"
                        startIcon={<ChatRoundedIcon />}
                        onClick={() => navigate(`/account/${accountIdNumber}/chat`)}
                    >
                        Chat
                    </Button>
                    <Button
                        variant="outlined"
                        size="small"
                        startIcon={<FeedbackRoundedIcon />}
                        onClick={() => navigate(`/account/${accountIdNumber}/feedback`)}
                    >
                        Feedback
                    </Button>
                    <Tooltip title="Refresh match history">
                        <IconButton
                            size="small"
                            onClick={handleCommit}
                            disabled={loading}
                            sx={{ border: '1px solid', borderColor: 'divider' }}
                        >
                            <RefreshRoundedIcon fontSize="small" />
                        </IconButton>
                    </Tooltip>
                </Stack>
            </Box>

            {/* Match History Card */}
            <Card variant="outlined">
                <Box sx={{ p: 2.5, pb: 0 }}>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                        <SportsEsportsRoundedIcon color="primary" />
                        <Typography variant="h6" sx={{ fontWeight: 600 }}>
                            Match History
                        </Typography>
                    </Box>
                    <Typography variant="body2" color="text.secondary">
                        Browse historical match records, played characters, scores, and statistics.
                    </Typography>
                </Box>
                <MatchHistory accountId={accountIdNumber ?? 0} />
            </Card>
        </Box>
    );
}

