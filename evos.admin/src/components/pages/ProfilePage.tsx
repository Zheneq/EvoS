import React, {useEffect, useMemo, useState} from 'react';
import {
    asDate,
    ban,
    clearQueuePenalty,
    formatDate,
    getPlayer,
    getStatus,
    mute,
    PlayerDetails,
    setVip,
    Status
} from "../../lib/Evos";
import {EvosError, processError} from "../../lib/Error";
import {useAuthHeader} from "react-auth-kit";
import {useNavigate, useParams} from "react-router-dom";
import Player from "../atlas/Player";
import {
    Alert,
    Box,
    Button,
    Card,
    Chip,
    Divider,
    Grid,
    IconButton,
    LinearProgress,
    Stack,
    Tooltip,
    Typography
} from "@mui/material";
import ErrorDialog from "../generic/ErrorDialog";
import MuteBanPlayer from "../controls/MuteBanPlayer";
import AdminMessages from "../controls/AdminMessages";
import TempPassword from "../controls/TempPassword";
import SendWhisper from "../controls/SendWhisper";
import BaseDialog from "../generic/BaseDialog";
import useInterval from "../../lib/useInterval";
import useHasFocus from "../../lib/useHasFocus";
import {useLocalStorage} from "../../lib/useLocalStorage";
import {Settings, SettingsKey} from "../../lib/Settings";
import StarRoundedIcon from "@mui/icons-material/StarRounded";
import StarBorderRoundedIcon from "@mui/icons-material/StarBorderRounded";
import SportsEsportsRoundedIcon from "@mui/icons-material/SportsEsportsRounded";
import ChatRoundedIcon from "@mui/icons-material/ChatRounded";
import FeedbackRoundedIcon from "@mui/icons-material/FeedbackRounded";
import VolumeOffRoundedIcon from "@mui/icons-material/VolumeOffRounded";
import GavelRoundedIcon from "@mui/icons-material/GavelRounded";
import BlockRoundedIcon from "@mui/icons-material/BlockRounded";
import CheckCircleRoundedIcon from "@mui/icons-material/CheckCircleRounded";
import WarningRoundedIcon from "@mui/icons-material/WarningRounded";
import RestartAltRoundedIcon from "@mui/icons-material/RestartAltRounded";
import RefreshRoundedIcon from "@mui/icons-material/RefreshRounded";
import AccessTimeFilledRoundedIcon from "@mui/icons-material/AccessTimeFilledRounded";

const UPDATE_PERIOD_MS = 20000;

export default function ProfilePage() {
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<EvosError>();
    const [playerDetails, setPlayerDetails] = useState<PlayerDetails>();
    const [status, setStatus] = useState<Status>();
    const [lastAction, setLastAction] = useState<Date>();
    const [confirmVipOpen, setConfirmVipOpen] = useState(false);
    const [vipProcessing, setVipProcessing] = useState(false);
    const [confirmClearQueueOpen, setConfirmClearQueueOpen] = useState(false);
    const [queueProcessing, setQueueProcessing] = useState(false);

    const {accountId} = useParams();
    const accountIdNumber = accountId ? parseInt(accountId) : undefined;

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
                document.title = `Account ${resp.data.player.handle}`;
                setLoading(false);
            })
            .catch((error) => processError(error, setError, navigate));

        getStatus(authHeader)
            .then((resp) => setStatus(resp.data))
            .catch((error) => processError(error, setError, navigate));

        return () => abort.abort();
    }, [accountIdNumber, authHeader, navigate, setPlayerDetails, lastAction]);

    const handle = playerDetails?.player.handle ?? "Nobody";
    const isVip = playerDetails?.isVip ?? false;
    const queueBlockedUntil = playerDetails?.queueBlockedUntil;
    const queueDodgeCount = playerDetails?.queueDodgeCount ?? 0;

    const mutedDate = asDate(playerDetails?.mutedUntil);
    const bannedDate = asDate(playerDetails?.bannedUntil);
    const blockedDate = asDate(queueBlockedUntil);

    const isMuted = !!mutedDate && mutedDate > new Date();
    const isBanned = !!bannedDate && bannedDate > new Date();
    const isQueueBlocked = !!blockedDate && blockedDate > new Date();

    const onlinePlayer = status?.players?.find(p => p.accountId === accountIdNumber);
    const rawBuildVersion = onlinePlayer?.buildVersion || playerDetails?.player.buildVersion;
    const cleanBuildVersion = rawBuildVersion
        ? rawBuildVersion.replace(/^STABLE-122-100_/, '')
        : 'Unknown';

    const isOnline = !!onlinePlayer;
    const currentStatus = onlinePlayer
        ? (onlinePlayer.status === "" ? "Online" : onlinePlayer.status)
        : (playerDetails?.player.status || "Offline");

    const displayPlayer = useMemo(() => {
        if (!playerDetails?.player) return undefined;
        return {
            ...playerDetails.player,
            ...(onlinePlayer ?? {}),
            buildVersion: rawBuildVersion ?? playerDetails.player.buildVersion,
            status: currentStatus,
        };
    }, [playerDetails, onlinePlayer, rawBuildVersion, currentStatus]);

    const handleClearQueueConfirm = () => {
        if (!accountIdNumber) return;
        setQueueProcessing(true);
        setConfirmClearQueueOpen(false);
        const abort = new AbortController();
        clearQueuePenalty(abort, authHeader, accountIdNumber)
            .then(() => handleCommit())
            .catch(e => processError(e, setError, navigate))
            .finally(() => setQueueProcessing(false));
    };

    const handleVipConfirm = () => {
        if (!accountIdNumber) return;
        setVipProcessing(true);
        setConfirmVipOpen(false);
        const abort = new AbortController();
        setVip(abort, authHeader, accountIdNumber, !isVip)
            .then(() => handleCommit())
            .catch(e => processError(e, setError, navigate))
            .finally(() => setVipProcessing(false));
    };

    return (
        <Box sx={{ width: '100%', display: 'flex', flexDirection: 'column', gap: 3 }}>
            {error && <ErrorDialog error={error} onDismiss={() => setError(undefined)} />}

            {loading && <LinearProgress sx={{ borderRadius: 1 }} />}

            {/* Dialogs */}
            <BaseDialog
                title={confirmVipOpen ? `${isVip ? 'Revoke' : 'Grant'} VIP for ${handle}?` : undefined}
                content={isVip ? `Revoke VIP status from ${handle}. The player will revert to standard account permissions.` : `Grant VIP status to ${handle}.`}
                onDismiss={() => setConfirmVipOpen(false)}
                onAccept={handleVipConfirm}
                acceptText={isVip ? 'Revoke VIP' : 'Grant VIP'}
            />
            <BaseDialog
                title={confirmClearQueueOpen ? `Clear queue penalty for ${handle}?` : undefined}
                content={`Clear active queue blocking and reset recorded dodge offenses (currently: ${queueDodgeCount}) for ${handle}.`}
                onDismiss={() => setConfirmClearQueueOpen(false)}
                onAccept={handleClearQueueConfirm}
                acceptText={'Clear Queue Penalty'}
            />

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
                        Player account profile, moderation controls, and communication tools.
                    </Typography>
                </Box>

                {/* Quick Actions */}
                <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap sx={{ alignItems: 'center' }}>
                    <Button
                        variant="outlined"
                        size="small"
                        startIcon={<SportsEsportsRoundedIcon />}
                        onClick={() => navigate(`/account/${accountIdNumber}/matches`)}
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
                    <Button
                        variant={isVip ? "outlined" : "contained"}
                        color={isVip ? "error" : "warning"}
                        size="small"
                        disabled={loading || vipProcessing}
                        startIcon={isVip ? <StarBorderRoundedIcon /> : <StarRoundedIcon />}
                        onClick={() => setConfirmVipOpen(true)}
                    >
                        {isVip ? 'Revoke VIP' : 'Grant VIP'}
                    </Button>
                    <Tooltip title="Refresh profile">
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

            {/* Player Banner & Identity Card */}
            <Card variant="outlined">
                <Box
                    sx={{
                        display: 'flex',
                        flexDirection: { xs: 'column', md: 'row' },
                        alignItems: { xs: 'flex-start', md: 'center' },
                        gap: 3,
                    }}
                >
                    <Box sx={{ flexShrink: 0 }}>
                        <Player info={displayPlayer} />
                    </Box>

                    <Divider orientation="vertical" flexItem sx={{ display: { xs: 'none', md: 'block' } }} />

                    <Grid container spacing={2} sx={{ flexGrow: 1 }}>
                        <Grid size={{ xs: 6, sm: 3 }}>
                            <Typography variant="caption" color="text.secondary" display="block">
                                ACCOUNT ID
                            </Typography>
                            <Typography variant="body1" sx={{ fontWeight: 600 }}>
                                {accountIdNumber ?? '—'}
                            </Typography>
                        </Grid>
                        <Grid size={{ xs: 6, sm: 3 }}>
                            <Typography variant="caption" color="text.secondary" display="block">
                                CONNECTION STATUS
                            </Typography>
                            <Typography variant="body1" sx={{ fontWeight: 600, color: isOnline ? 'success.main' : 'text.secondary' }}>
                                {currentStatus}
                            </Typography>
                        </Grid>
                        <Grid size={{ xs: 6, sm: 3 }}>
                            <Typography variant="caption" color="text.secondary" display="block">
                                CLIENT VERSION
                            </Typography>
                            <Typography variant="body1" sx={{ fontWeight: 600 }}>
                                {cleanBuildVersion}
                            </Typography>
                        </Grid>
                        <Grid size={{ xs: 6, sm: 3 }}>
                            <Typography variant="caption" color="text.secondary" display="block">
                                VIP STATUS
                            </Typography>
                            <Typography variant="body1" sx={{ fontWeight: 600, color: isVip ? 'warning.main' : 'text.primary' }}>
                                {isVip ? 'Active VIP' : 'Standard'}
                            </Typography>
                        </Grid>
                    </Grid>
                </Box>
            </Card>

            {/* Management Cards Grid */}
            <Grid container spacing={2.5}>
                {/* Column 1: Moderation & Queue */}
                <Grid size={{ xs: 12, lg: 6 }}>
                    <Stack spacing={2.5}>
                        {/* Queue Penalty Card */}
                        <Card variant="outlined">
                            <Stack spacing={2} sx={{ width: '100%' }}>
                                <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 1 }}>
                                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                                        <AccessTimeFilledRoundedIcon color={isQueueBlocked ? "error" : "primary"} />
                                        <Typography variant="h6" sx={{ fontWeight: 600 }}>
                                            Queue Standing & Penalty
                                        </Typography>
                                    </Box>
                                    {isQueueBlocked && (
                                        <Chip label="Blocked" color="error" size="small" />
                                    )}
                                </Box>

                                {isQueueBlocked ? (
                                    <Alert severity="error" icon={<BlockRoundedIcon />}>
                                        <Typography variant="body2" sx={{ fontWeight: 600 }}>
                                            Queue blocked until {formatDate(queueBlockedUntil!)}
                                        </Typography>
                                        <Typography variant="caption" color="text.secondary">
                                            Dodge offenses recorded: {queueDodgeCount}
                                        </Typography>
                                    </Alert>
                                ) : queueDodgeCount > 0 ? (
                                    <Alert severity="warning" icon={<WarningRoundedIcon />}>
                                        <Typography variant="body2" sx={{ fontWeight: 600 }}>
                                            No active queue block
                                        </Typography>
                                        <Typography variant="caption" color="text.secondary">
                                            {queueDodgeCount} dodge offense{queueDodgeCount > 1 ? 's' : ''} on record.
                                        </Typography>
                                    </Alert>
                                ) : (
                                    <Alert severity="success" icon={<CheckCircleRoundedIcon />}>
                                        <Typography variant="body2" sx={{ fontWeight: 600 }}>
                                            Clean Standing
                                        </Typography>
                                        <Typography variant="caption" color="text.secondary">
                                            No active queue penalties or recorded offenses.
                                        </Typography>
                                    </Alert>
                                )}

                                <Box sx={{ display: 'flex', justifyContent: 'flex-start' }}>
                                    <Button
                                        variant="outlined"
                                        color="warning"
                                        startIcon={<RestartAltRoundedIcon />}
                                        disabled={loading || queueProcessing || (!queueBlockedUntil && queueDodgeCount === 0)}
                                        onClick={() => setConfirmClearQueueOpen(true)}
                                    >
                                        Clear Queue Penalty & Offenses
                                    </Button>
                                </Box>

                                {queueProcessing && <LinearProgress sx={{ borderRadius: 1 }} />}
                            </Stack>
                        </Card>

                        {/* Mute Player Card */}
                        <MuteBanPlayer
                            disabled={loading}
                            deadline={asDate(playerDetails?.mutedUntil)}
                            accountId={playerDetails?.player.accountId ?? 0}
                            action={mute}
                            handle={handle}
                            actionText={"mute"}
                            doneText={"muted"}
                            onCommit={handleCommit}
                        />

                        {/* Ban Player Card */}
                        <MuteBanPlayer
                            disabled={loading}
                            deadline={asDate(playerDetails?.bannedUntil)}
                            accountId={playerDetails?.player.accountId ?? 0}
                            action={ban}
                            handle={handle}
                            actionText={"ban"}
                            doneText={"banned"}
                            onCommit={handleCommit}
                        />
                    </Stack>
                </Grid>

                {/* Column 2: Communication & Credentials */}
                <Grid size={{ xs: 12, lg: 6 }}>
                    <Stack spacing={2.5}>
                        {/* Send In-Game Whisper */}
                        <SendWhisper
                            accountId={playerDetails?.player.accountId ?? 0}
                            handle={handle}
                        />

                        {/* Temporary Password */}
                        <TempPassword
                            accountId={playerDetails?.player.accountId ?? 0}
                        />
                    </Stack>
                </Grid>

                {/* Full-Width Row: Admin Direct Messages */}
                <Grid size={{ xs: 12 }}>
                    <AdminMessages
                        accountId={playerDetails?.player.accountId ?? 0}
                    />
                </Grid>
            </Grid>
        </Box>
    );
}

