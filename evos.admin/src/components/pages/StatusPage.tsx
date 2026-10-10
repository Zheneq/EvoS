import React, {useCallback, useMemo, useState} from 'react';
import {getStatus, Status} from "../../lib/Evos";
import {
    alpha,
    Box,
    Card,
    Chip,
    Grid,
    IconButton,
    LinearProgress,
    Paper,
    Stack,
    Tab,
    Tabs,
    Tooltip,
    Typography,
} from "@mui/material";
import Queue from "../atlas/Queue";
import {useAuthHeader} from "react-auth-kit";
import Server from "../atlas/Server";
import {useNavigate} from "react-router-dom";
import {EvosError, processError} from "../../lib/Error";
import ErrorDialog from "../generic/ErrorDialog";
import useInterval from "../../lib/useInterval";
import useHasFocus from "../../lib/useHasFocus";
import {useLocalStorage} from "../../lib/useLocalStorage";
import {Settings, SettingsKey} from "../../lib/Settings";
import DnsRoundedIcon from "@mui/icons-material/DnsRounded";
import SportsEsportsRoundedIcon from "@mui/icons-material/SportsEsportsRounded";
import PersonRoundedIcon from "@mui/icons-material/PersonRounded";
import HourglassEmptyRoundedIcon from "@mui/icons-material/HourglassEmptyRounded";
import RefreshRoundedIcon from "@mui/icons-material/RefreshRounded";
import FiberManualRecordRoundedIcon from "@mui/icons-material/FiberManualRecordRounded";
import GroupRoundedIcon from "@mui/icons-material/GroupRounded";
import StorageRoundedIcon from "@mui/icons-material/StorageRounded";

function GroupBy<V, K>(key: (item: V) => K, list?: V[]) {
    return list?.reduce((res, p) => {
        res.set(key(p), p);
        return res;
    }, new Map<K, V>());
}

function RoundToNearest5(x: number) {
    return Math.round(x / 5) * 5;
}

function FormatAge(ageMs: number) {
    if (ageMs < 5000) {
        return 'just now';
    }
    if (ageMs < 60000) {
        return `${RoundToNearest5(ageMs / 1000)}s ago`;
    }
    if (ageMs < 90000) {
        return `1m ago`;
    }
    return `${Math.round(ageMs / 60000)}m ago`;
}

const UPDATE_PERIOD_MS = 20000;

type StatusTab = 'overview' | 'games' | 'queues' | 'lobby' | 'servers';

function StatusPage() {
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<EvosError>();
    const [status, setStatus] = useState<Status>();
    const [updateTime, setUpdateTime] = useState<Date>();
    const [age, setAge] = useState<number>();
    const [activeTab, setActiveTab] = useState<StatusTab>('overview');

    const authHeader = useAuthHeader()();
    const navigate = useNavigate();

    const players = useMemo(() => GroupBy(p => p.accountId, status?.players), [status]);
    const groups = useMemo(() => GroupBy(g => g.groupId, status?.groups), [status]);
    const games = useMemo(() => GroupBy(g => g.server, status?.games), [status]);

    const [updateInBackground] = useLocalStorage(Settings.get(SettingsKey.updateInBackground)!);
    const hasFocus = useHasFocus();
    const updatePeriodMs = updateInBackground || hasFocus || !status ? UPDATE_PERIOD_MS : undefined;

    const fetchStatus = useCallback(() => {
        setLoading(true);
        getStatus(authHeader)
            .then((resp) => {
                setStatus(resp.data);
                setUpdateTime(new Date());
                setAge(0);
                setError(undefined);
            })
            .catch((error) => processError(error, setError, navigate))
            .finally(() => setLoading(false));
    }, [authHeader, navigate]);

    useInterval(() => {
        fetchStatus();
    }, updatePeriodMs);

    useInterval(() => {
        if (updateTime) {
            setAge(new Date().getTime() - updateTime.getTime());
        }
    }, 5000);

    const queuedGroups = useMemo(() => new Set(status?.queues?.flatMap(q => q.groupIds || [])), [status]);
    const notQueuedGroups = useMemo(() => groups ? [...groups.keys()].filter(g => !queuedGroups.has(g)) : [], [groups, queuedGroups]);
    const inGame = useMemo(() => {
        if (!games) return new Set<number>();
        return new Set([...games.values()].flatMap(g => [...g.teamA, ...g.teamB]).map(t => t.accountId));
    }, [games]);

    const activeServers = useMemo(() => {
        if (!status || !games) return [];
        return status.servers
            .filter(s => games.get(s.id))
            .sort((s1, s2) => s1.name.localeCompare(s2.name));
    }, [status, games]);

    const idleServers = useMemo(() => {
        if (!status || !games) return [];
        return status.servers
            .filter(s => !games.get(s.id))
            .sort((s1, s2) => s1.name.localeCompare(s2.name));
    }, [status, games]);

    const activeQueues = useMemo(() => {
        if (!status) return [];
        return status.queues.filter(q => q.groupIds && q.groupIds.length > 0);
    }, [status]);

    const totalQueuedPlayers = useMemo(() => {
        if (!status?.queues || !groups) return 0;
        const groupIds = status.queues.flatMap(q => q.groupIds || []);
        const playerIds = new Set<number>();
        groupIds.forEach(gid => {
            const grp = groups.get(gid);
            grp?.accountIds?.forEach(aid => playerIds.add(aid));
        });
        return playerIds.size;
    }, [status, groups]);

    const notQueuedPlayersCount = useMemo(() => {
        if (!notQueuedGroups || !groups || !inGame) return 0;
        let count = 0;
        notQueuedGroups.forEach(gid => {
            const grp = groups.get(gid);
            grp?.accountIds?.forEach(aid => {
                if (!inGame.has(aid)) count++;
            });
        });
        return count;
    }, [notQueuedGroups, groups, inGame]);

    const handleTabChange = (_event: React.SyntheticEvent, newValue: StatusTab) => {
        setActiveTab(newValue);
    };

    return (
        <Box sx={{ width: '100%', display: 'flex', flexDirection: 'column', gap: 3 }}>
            {error && <ErrorDialog error={error} onDismiss={() => setError(undefined)} />}

            {/* Header Toolbar */}
            <Box
                sx={{
                    display: 'flex',
                    flexDirection: { xs: 'column', sm: 'row' },
                    justifyContent: 'space-between',
                    alignItems: { xs: 'flex-start', sm: 'center' },
                    gap: 2,
                    pb: 2,
                    borderBottom: '1px solid',
                    borderColor: 'divider',
                }}
            >
                <Box>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mb: 0.5 }}>
                        <DnsRoundedIcon color="primary" sx={{ fontSize: 32 }} />
                        <Typography variant="h4" component="h1" sx={{ fontWeight: 700, letterSpacing: '-0.5px' }}>
                            Cluster Status
                        </Typography>
                    </Box>
                    <Typography variant="body2" color="text.secondary">
                        Live real-time monitoring of matches, matchmaking queues, players, and game servers.
                    </Typography>
                </Box>

                <Stack direction="row" spacing={1.5} alignItems="center">
                    <Chip
                        icon={
                            <FiberManualRecordRoundedIcon
                                sx={{
                                    fontSize: '10px !important',
                                    color: '#4caf50',
                                    animation: 'pulse 2s infinite ease-in-out',
                                    '@keyframes pulse': {
                                        '0%': { opacity: 1, transform: 'scale(1)' },
                                        '50%': { opacity: 0.4, transform: 'scale(1.2)' },
                                        '100%': { opacity: 1, transform: 'scale(1)' },
                                    },
                                }}
                            />
                        }
                        label={age === undefined ? 'Connecting...' : `Live • Updated ${FormatAge(age)}`}
                        size="small"
                        variant="outlined"
                        sx={{ fontWeight: 600, borderColor: 'divider' }}
                    />

                    <Tooltip title="Refresh cluster status">
                        <span>
                            <IconButton
                                size="small"
                                onClick={fetchStatus}
                                disabled={loading}
                                sx={{ border: '1px solid', borderColor: 'divider' }}
                            >
                                <RefreshRoundedIcon
                                    fontSize="small"
                                    sx={{
                                        animation: loading ? 'spin 1s linear infinite' : 'none',
                                        '@keyframes spin': {
                                            '0%': { transform: 'rotate(0deg)' },
                                            '100%': { transform: 'rotate(360deg)' },
                                        },
                                    }}
                                />
                            </IconButton>
                        </span>
                    </Tooltip>
                </Stack>
            </Box>

            {loading && !status && <LinearProgress />}

            {/* KPI Metric Summary Cards */}
            {status && (
                <Grid container spacing={1.5}>
                    <Grid size={{ xs: 12, sm: 6, md: 2.4 }}>
                        <Card variant="outlined" sx={{ p: 2, display: 'flex', alignItems: 'center', gap: 1.5 }}>
                            <Box
                                sx={{
                                    p: 1.25,
                                    borderRadius: 1.5,
                                    display: 'flex',
                                    alignItems: 'center',
                                    justifyContent: 'center',
                                    bgcolor: (theme) => alpha(theme.palette.primary.main, 0.1),
                                    color: 'primary.main',
                                }}
                            >
                                <PersonRoundedIcon />
                            </Box>
                            <Box>
                                <Typography variant="caption" color="text.secondary" fontWeight={500}>
                                    Online Players
                                </Typography>
                                <Typography variant="h6" fontWeight={700} lineHeight={1.2}>
                                    {status.players.length}
                                </Typography>
                            </Box>
                        </Card>
                    </Grid>

                    <Grid size={{ xs: 12, sm: 6, md: 2.4 }}>
                        <Card variant="outlined" sx={{ p: 2, display: 'flex', alignItems: 'center', gap: 1.5 }}>
                            <Box
                                sx={{
                                    p: 1.25,
                                    borderRadius: 1.5,
                                    display: 'flex',
                                    alignItems: 'center',
                                    justifyContent: 'center',
                                    bgcolor: (theme) => alpha(theme.palette.success.main, 0.1),
                                    color: 'success.main',
                                }}
                            >
                                <SportsEsportsRoundedIcon />
                            </Box>
                            <Box>
                                <Typography variant="caption" color="text.secondary" fontWeight={500}>
                                    In Matches
                                </Typography>
                                <Stack direction="row" spacing={0.5} alignItems="baseline">
                                    <Typography variant="h6" fontWeight={700} lineHeight={1.2}>
                                        {inGame.size}
                                    </Typography>
                                    <Typography variant="caption" color="text.secondary">
                                        ({activeServers.length} games)
                                    </Typography>
                                </Stack>
                            </Box>
                        </Card>
                    </Grid>

                    <Grid size={{ xs: 12, sm: 6, md: 2.4 }}>
                        <Card variant="outlined" sx={{ p: 2, display: 'flex', alignItems: 'center', gap: 1.5 }}>
                            <Box
                                sx={{
                                    p: 1.25,
                                    borderRadius: 1.5,
                                    display: 'flex',
                                    alignItems: 'center',
                                    justifyContent: 'center',
                                    bgcolor: (theme) => alpha(theme.palette.warning.main, 0.1),
                                    color: 'warning.main',
                                }}
                            >
                                <HourglassEmptyRoundedIcon />
                            </Box>
                            <Box>
                                <Typography variant="caption" color="text.secondary" fontWeight={500}>
                                    In Queues
                                </Typography>
                                <Stack direction="row" spacing={0.5} alignItems="baseline">
                                    <Typography variant="h6" fontWeight={700} lineHeight={1.2}>
                                        {totalQueuedPlayers}
                                    </Typography>
                                    <Typography variant="caption" color="text.secondary">
                                        ({queuedGroups.size} groups)
                                    </Typography>
                                </Stack>
                            </Box>
                        </Card>
                    </Grid>

                    <Grid size={{ xs: 12, sm: 6, md: 2.4 }}>
                        <Card variant="outlined" sx={{ p: 2, display: 'flex', alignItems: 'center', gap: 1.5 }}>
                            <Box
                                sx={{
                                    p: 1.25,
                                    borderRadius: 1.5,
                                    display: 'flex',
                                    alignItems: 'center',
                                    justifyContent: 'center',
                                    bgcolor: (theme) => alpha(theme.palette.info.main, 0.1),
                                    color: 'info.main',
                                }}
                            >
                                <GroupRoundedIcon />
                            </Box>
                            <Box>
                                <Typography variant="caption" color="text.secondary" fontWeight={500}>
                                    Lobby / Idle
                                </Typography>
                                <Stack direction="row" spacing={0.5} alignItems="baseline">
                                    <Typography variant="h6" fontWeight={700} lineHeight={1.2}>
                                        {notQueuedPlayersCount}
                                    </Typography>
                                    <Typography variant="caption" color="text.secondary">
                                        ({notQueuedGroups.length} groups)
                                    </Typography>
                                </Stack>
                            </Box>
                        </Card>
                    </Grid>

                    <Grid size={{ xs: 12, sm: 6, md: 2.4 }}>
                        <Card variant="outlined" sx={{ p: 2, display: 'flex', alignItems: 'center', gap: 1.5 }}>
                            <Box
                                sx={{
                                    p: 1.25,
                                    borderRadius: 1.5,
                                    display: 'flex',
                                    alignItems: 'center',
                                    justifyContent: 'center',
                                    bgcolor: (theme) => alpha(theme.palette.secondary.main, 0.1),
                                    color: 'secondary.main',
                                }}
                            >
                                <StorageRoundedIcon />
                            </Box>
                            <Box>
                                <Typography variant="caption" color="text.secondary" fontWeight={500}>
                                    Dedicated Servers
                                </Typography>
                                <Typography variant="h6" fontWeight={700} lineHeight={1.2}>
                                    {status.servers.length}{' '}
                                    <Typography component="span" variant="caption" color="text.secondary">
                                        ({activeServers.length} active)
                                    </Typography>
                                </Typography>
                            </Box>
                        </Card>
                    </Grid>
                </Grid>
            )}

            {/* Navigation Tabs */}
            <Tabs
                value={activeTab}
                onChange={handleTabChange}
                variant="scrollable"
                scrollButtons="auto"
                sx={{ borderBottom: 1, borderColor: 'divider' }}
            >
                <Tab
                    value="overview"
                    label="All Overview"
                    sx={{ textTransform: 'none', fontWeight: 600 }}
                />
                <Tab
                    value="games"
                    icon={<SportsEsportsRoundedIcon sx={{ fontSize: 18 }} />}
                    iconPosition="start"
                    label={`Active Matches (${activeServers.length})`}
                    sx={{ textTransform: 'none', fontWeight: 600 }}
                />
                <Tab
                    value="queues"
                    icon={<HourglassEmptyRoundedIcon sx={{ fontSize: 18 }} />}
                    iconPosition="start"
                    label={`Queues (${activeQueues.length})`}
                    sx={{ textTransform: 'none', fontWeight: 600 }}
                />
                <Tab
                    value="lobby"
                    icon={<GroupRoundedIcon sx={{ fontSize: 18 }} />}
                    iconPosition="start"
                    label={`Lobby (${notQueuedGroups.length})`}
                    sx={{ textTransform: 'none', fontWeight: 600 }}
                />
                <Tab
                    value="servers"
                    icon={<DnsRoundedIcon sx={{ fontSize: 18 }} />}
                    iconPosition="start"
                    label={`Servers (${status?.servers?.length ?? 0})`}
                    sx={{ textTransform: 'none', fontWeight: 600 }}
                />
            </Tabs>

            {/* TAB CONTENT */}

            {/* 1. Active Matches Section */}
            {(activeTab === 'overview' || activeTab === 'games') && (
                <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                    <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                            <SportsEsportsRoundedIcon color="primary" />
                            <Typography variant="h6" fontWeight={700}>
                                Active Matches
                            </Typography>
                            <Chip label={activeServers.length} size="small" color="primary" sx={{ height: 20, fontWeight: 700 }} />
                        </Box>
                    </Box>

                    {activeServers.length === 0 ? (
                        <Card variant="outlined" sx={{ p: 4, textAlign: 'center', backgroundColor: (theme) => alpha(theme.palette.action.hover, 0.2) }}>
                            <SportsEsportsRoundedIcon sx={{ fontSize: 40, color: 'text.secondary', opacity: 0.5, mb: 1 }} />
                            <Typography variant="body1" fontWeight={600}>
                                No Active Matches
                            </Typography>
                            <Typography variant="body2" color="text.secondary">
                                All game servers are currently idle and ready to spin up matches.
                            </Typography>
                        </Card>
                    ) : (
                        <Stack spacing={2}>
                            {activeServers.map((s) => (
                                <Card key={s.id} variant="outlined" sx={{ p: 2 }}>
                                    <Server info={s} game={games?.get(s.id)} playerData={players!} />
                                </Card>
                            ))}
                        </Stack>
                    )}
                </Box>
            )}

            {/* 2. Matchmaking Queues Section */}
            {(activeTab === 'overview' || activeTab === 'queues') && (
                <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                    <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                            <HourglassEmptyRoundedIcon color="warning" />
                            <Typography variant="h6" fontWeight={700}>
                                Matchmaking Queues
                            </Typography>
                            <Chip label={`${totalQueuedPlayers} waiting`} size="small" color="warning" variant="outlined" sx={{ height: 20, fontWeight: 700 }} />
                        </Box>
                    </Box>

                    {activeQueues.length === 0 ? (
                        <Card variant="outlined" sx={{ p: 4, textAlign: 'center', backgroundColor: (theme) => alpha(theme.palette.action.hover, 0.2) }}>
                            <HourglassEmptyRoundedIcon sx={{ fontSize: 40, color: 'text.secondary', opacity: 0.5, mb: 1 }} />
                            <Typography variant="body1" fontWeight={600}>
                                Queues Are Empty
                            </Typography>
                            <Typography variant="body2" color="text.secondary">
                                No players are currently in matchmaking queues.
                            </Typography>
                        </Card>
                    ) : (
                        <Stack spacing={2}>
                            {activeQueues.map((q) => (
                                <Card key={`${q.type}_${q.subtype}`} variant="outlined" sx={{ p: 2.5 }}>
                                    <Queue info={q} groupData={groups!} playerData={players!} hidePlayers={inGame} />
                                </Card>
                            ))}
                        </Stack>
                    )}
                </Box>
            )}

            {/* 3. Lobby Groups Section */}
            {(activeTab === 'overview' || activeTab === 'lobby') && (
                <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                    <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                            <GroupRoundedIcon color="info" />
                            <Typography variant="h6" fontWeight={700}>
                                Lobby Players & Groups
                            </Typography>
                            <Chip label={`${notQueuedPlayersCount} players`} size="small" color="info" variant="outlined" sx={{ height: 20, fontWeight: 700 }} />
                        </Box>
                    </Box>

                    {notQueuedGroups.length === 0 ? (
                        <Card variant="outlined" sx={{ p: 4, textAlign: 'center', backgroundColor: (theme) => alpha(theme.palette.action.hover, 0.2) }}>
                            <GroupRoundedIcon sx={{ fontSize: 40, color: 'text.secondary', opacity: 0.5, mb: 1 }} />
                            <Typography variant="body1" fontWeight={600}>
                                Lobby is Empty
                            </Typography>
                            <Typography variant="body2" color="text.secondary">
                                All connected players are currently in game or queueing.
                            </Typography>
                        </Card>
                    ) : (
                        <Card variant="outlined" sx={{ p: 2.5 }}>
                            <Queue
                                key="not_queued"
                                info={{ type: "Lobby Groups", subtype: "(Not Queued)", groupIds: notQueuedGroups }}
                                groupData={groups!}
                                playerData={players!}
                                hidePlayers={inGame}
                            />
                        </Card>
                    )}
                </Box>
            )}

            {/* 4. Dedicated Server Pool Section */}
            {(activeTab === 'overview' || activeTab === 'servers') && status && (
                <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                    <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                            <DnsRoundedIcon color="secondary" />
                            <Typography variant="h6" fontWeight={700}>
                                Dedicated Server Fleet
                            </Typography>
                            <Chip
                                label={`${activeServers.length} Active / ${idleServers.length} Idle`}
                                size="small"
                                color="secondary"
                                variant="outlined"
                                sx={{ height: 20, fontWeight: 700 }}
                            />
                        </Box>
                    </Box>

                    <Grid container spacing={1.5}>
                        {status.servers.map((s) => {
                            const isRunningGame = !!games?.get(s.id);
                            const game = games?.get(s.id);
                            return (
                                <Grid key={s.id} size={{ xs: 12, sm: 6, md: 4, lg: 3 }}>
                                    <Paper
                                        variant="outlined"
                                        sx={{
                                            p: 1.75,
                                            display: 'flex',
                                            flexDirection: 'column',
                                            gap: 1,
                                            borderRadius: 2,
                                            backgroundColor: (theme) =>
                                                isRunningGame
                                                    ? alpha(theme.palette.primary.main, 0.04)
                                                    : alpha(theme.palette.primary.main, 0.1),
                                            borderLeft: '4px solid',
                                            borderLeftColor: isRunningGame ? 'primary.main' : 'success.main',
                                        }}
                                    >
                                        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                                            <Stack direction="row" spacing={1} alignItems="center">
                                                <StorageRoundedIcon
                                                    sx={{
                                                        fontSize: 20,
                                                        color: isRunningGame ? 'primary.main' : 'text.secondary',
                                                    }}
                                                />
                                                <Typography variant="body2" fontWeight={700}>
                                                    {s.name}
                                                </Typography>
                                            </Stack>
                                            <Chip
                                                label={isRunningGame ? "In Match" : "Ready"}
                                                size="small"
                                                color={isRunningGame ? "primary" : "success"}
                                                variant={isRunningGame ? "filled" : "outlined"}
                                                sx={{ height: 20, fontSize: '0.675rem', fontWeight: 700 }}
                                            />
                                        </Box>
                                        <Typography variant="caption" color="text.secondary" sx={{ fontFamily: 'monospace' }}>
                                            ID: {s.id}
                                        </Typography>
                                        {game && (
                                            <Typography variant="caption" color="primary" fontWeight={600}>
                                                {game.gameType} • {game.gameSubType?.split('@')[0]}
                                            </Typography>
                                        )}
                                    </Paper>
                                </Grid>
                            );
                        })}
                    </Grid>
                </Box>
            )}
        </Box>
    );
}

export default StatusPage;
