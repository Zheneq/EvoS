import React, {useEffect, useMemo, useState} from 'react';
import {
    alpha,
    Box,
    Button,
    Card,
    Chip,
    CircularProgress,
    FormControl,
    Grid,
    IconButton,
    InputAdornment,
    InputLabel,
    MenuItem,
    Paper,
    Select,
    SelectChangeEvent,
    Stack,
    Tab,
    Table,
    TableBody,
    TableCell,
    TableContainer,
    TableHead,
    TablePagination,
    TableRow,
    Tabs,
    TextField,
    Tooltip,
    Typography,
} from '@mui/material';
import {
    CharacterType,
    formatDate,
    formatRelativeTime,
    getMatchHistory,
    MatchHistoryEntry,
    PlayerGameResult,
    Team,
} from "../../lib/Evos";
import {useAuthHeader} from "react-auth-kit";
import {EvosError, processError} from "../../lib/Error";
import {useNavigate, useSearchParams} from "react-router-dom";
import ErrorDialog from "../generic/ErrorDialog";
import {CharacterIcon} from "../atlas/CharacterIcon";
import HistoryNavButtons from "../generic/HistoryNavButtons";
import {useBeforeParamState, useDateParamState} from "../../lib/Lib";
import SportsEsportsRoundedIcon from "@mui/icons-material/SportsEsportsRounded";
import SearchRoundedIcon from "@mui/icons-material/SearchRounded";
import CloseRoundedIcon from "@mui/icons-material/CloseRounded";
import EmojiEventsRoundedIcon from "@mui/icons-material/EmojiEventsRounded";
import CancelRoundedIcon from "@mui/icons-material/CancelRounded";
import OpenInNewRoundedIcon from "@mui/icons-material/OpenInNewRounded";
import HourglassEmptyRoundedIcon from "@mui/icons-material/HourglassEmptyRounded";
import LeaderboardRoundedIcon from "@mui/icons-material/LeaderboardRounded";
import MapRoundedIcon from "@mui/icons-material/MapRounded";

interface MatchHistoryProps {
    accountId: number;
}

type ResultTab = 'all' | 'win' | 'loss' | 'tie';

const LIMIT = 50;

export const MatchHistory: React.FC<MatchHistoryProps> = ({accountId}: MatchHistoryProps) => {
    const [searchParams, setSearchParams] = useSearchParams();
    const [matches, setMatches] = useState<MatchHistoryEntry[]>([]);
    const [loading, setLoading] = useState(true);

    const [date, setDate] = useDateParamState(searchParams);
    const [isBefore, setIsBefore] = useBeforeParamState(searchParams);

    // Filters and pagination
    const [resultFilter, setResultFilter] = useState<ResultTab>('all');
    const [mapFilter, setMapFilter] = useState('all');
    const [searchQuery, setSearchQuery] = useState('');
    const [page, setPage] = useState(0);
    const [rowsPerPage, setRowsPerPage] = useState(10);

    const [error, setError] = useState<EvosError>();
    const authHeader = useAuthHeader()();
    const navigate = useNavigate();

    useEffect(() => {
        const newParams = new URLSearchParams(searchParams);
        newParams.set('before', isBefore.toString());
        newParams.set('ts', Math.floor(date.unix()).toString());
        setSearchParams(newParams);
        // eslint-disable-next-line
    }, [date, isBefore]);

    useEffect(() => {
        if (accountId === 0) {
            setLoading(false);
            return;
        }

        setLoading(true);
        const abort = new AbortController();
        const timestamp = Math.floor(date.unix());

        getMatchHistory(abort, authHeader, accountId, timestamp, isBefore, LIMIT)
            .then((resp) => {
                setMatches(resp.data.matches || []);
                setPage(0);
            })
            .catch((error) => {
                if (!abort.signal.aborted) {
                    processError(error, setError, navigate);
                }
            })
            .finally(() => {
                if (!abort.signal.aborted) {
                    setLoading(false);
                }
            });

        return () => abort.abort();
    }, [accountId, authHeader, date, isBefore, navigate]);

    // Match metrics computed over current batch
    const stats = useMemo(() => {
        if (!matches || matches.length === 0) {
            return { total: 0, wins: 0, losses: 0, ties: 0, winRate: 0, avgTurns: 0, topCharacter: null as CharacterType | null, charCount: 0 };
        }
        const total = matches.length;
        const wins = matches.filter(m => m.result === PlayerGameResult.Win).length;
        const losses = matches.filter(m => m.result === PlayerGameResult.Lose).length;
        const ties = matches.filter(m => m.result === PlayerGameResult.Tie).length;
        const winRate = (wins + losses) > 0 ? Math.round((wins / (wins + losses)) * 100) : 0;
        const totalTurns = matches.reduce((sum, m) => sum + (m.numOfTurns || 0), 0);
        const avgTurns = Math.round((totalTurns / total) * 10) / 10;

        const charCounts = new Map<CharacterType, number>();
        matches.forEach(m => {
            if (m.character) {
                charCounts.set(m.character, (charCounts.get(m.character) || 0) + 1);
            }
        });
        let topChar: CharacterType | null = null;
        let maxCount = 0;
        charCounts.forEach((count, char) => {
            if (count > maxCount) {
                maxCount = count;
                topChar = char;
            }
        });

        return { total, wins, losses, ties, winRate, avgTurns, topCharacter: topChar, charCount: maxCount };
    }, [matches]);

    const availableMaps = useMemo(() => {
        const set = new Set<string>();
        matches.forEach(m => {
            if (m.mapName) set.add(m.mapName);
        });
        return Array.from(set).sort();
    }, [matches]);

    const filteredMatches = useMemo(() => {
        const list = [...matches].reverse();
        return list.filter(m => {
            if (resultFilter === 'win' && m.result !== PlayerGameResult.Win) return false;
            if (resultFilter === 'loss' && m.result !== PlayerGameResult.Lose) return false;
            if (resultFilter === 'tie' && m.result !== PlayerGameResult.Tie) return false;

            if (mapFilter !== 'all' && m.mapName !== mapFilter) return false;

            if (searchQuery.trim()) {
                const q = searchQuery.toLowerCase().trim();
                const char = (m.character || '').toLowerCase();
                const map = (m.mapName || '').toLowerCase();
                const gameType = (m.gameType || '').toLowerCase();
                const subType = (m.subType || '').toLowerCase();
                const matchId = (m.matchId || '').toLowerCase();

                if (
                    !char.includes(q) &&
                    !map.includes(q) &&
                    !gameType.includes(q) &&
                    !subType.includes(q) &&
                    !matchId.includes(q)
                ) {
                    return false;
                }
            }

            return true;
        });
    }, [matches, resultFilter, mapFilter, searchQuery]);

    const paginatedMatches = useMemo(() => {
        return filteredMatches.slice(page * rowsPerPage, page * rowsPerPage + rowsPerPage);
    }, [filteredMatches, page, rowsPerPage]);

    const handleTabChange = (_event: React.SyntheticEvent, newValue: ResultTab) => {
        setResultFilter(newValue);
        setPage(0);
    };

    const handleSearchChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        setSearchQuery(e.target.value);
        setPage(0);
    };

    const handleMapChange = (e: SelectChangeEvent) => {
        setMapFilter(e.target.value);
        setPage(0);
    };

    const hasActiveFilters = searchQuery.trim() !== '' || mapFilter !== 'all' || resultFilter !== 'all';

    const clearFilters = () => {
        setSearchQuery('');
        setMapFilter('all');
        setResultFilter('all');
        setPage(0);
    };

    const renderResultChip = (result: PlayerGameResult) => {
        switch (result) {
            case PlayerGameResult.Win:
                return (
                    <Chip
                        label="VICTORY"
                        size="small"
                        color="success"
                        icon={<EmojiEventsRoundedIcon sx={{ fontSize: '14px !important' }} />}
                        sx={{ fontWeight: 700, minWidth: 88, letterSpacing: '0.04em' }}
                    />
                );
            case PlayerGameResult.Lose:
                return (
                    <Chip
                        label="DEFEAT"
                        size="small"
                        color="error"
                        icon={<CancelRoundedIcon sx={{ fontSize: '14px !important' }} />}
                        sx={{ fontWeight: 700, minWidth: 88, letterSpacing: '0.04em' }}
                    />
                );
            case PlayerGameResult.Tie:
                return (
                    <Chip
                        label="DRAW"
                        size="small"
                        color="warning"
                        variant="outlined"
                        sx={{ fontWeight: 700, minWidth: 88, letterSpacing: '0.04em' }}
                    />
                );
            default:
                return (
                    <Chip
                        label="NO RESULT"
                        size="small"
                        variant="outlined"
                        sx={{ fontWeight: 600, minWidth: 88 }}
                    />
                );
        }
    };

    const getRowBg = (result: PlayerGameResult, theme: any) => {
        switch (result) {
            case PlayerGameResult.Win:
                return alpha(theme.palette.success.main, 0.05);
            case PlayerGameResult.Lose:
                return alpha(theme.palette.error.main, 0.05);
            case PlayerGameResult.Tie:
                return alpha(theme.palette.warning.main, 0.05);
            default:
                return 'transparent';
        }
    };

    const getRowHoverBg = (result: PlayerGameResult, theme: any) => {
        switch (result) {
            case PlayerGameResult.Win:
                return `${alpha(theme.palette.success.main, 0.12)} !important`;
            case PlayerGameResult.Lose:
                return `${alpha(theme.palette.error.main, 0.12)} !important`;
            case PlayerGameResult.Tie:
                return `${alpha(theme.palette.warning.main, 0.12)} !important`;
            default:
                return `${alpha(theme.palette.action.hover, 0.1)} !important`;
        }
    };

    return (
        <Box sx={{ width: '100%', p: 2 }}>
            {error && <ErrorDialog error={error} onDismiss={() => setError(undefined)} />}

            {/* KPI Performance Cards */}
            {!loading && matches.length > 0 && (
                <Grid container spacing={1.5} sx={{ mb: 2.5 }}>
                    <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                        <Card variant="outlined" sx={{ p: 1.75, display: 'flex', alignItems: 'center', gap: 1.5 }}>
                            <Box
                                sx={{
                                    p: 1,
                                    borderRadius: 1.5,
                                    display: 'flex',
                                    alignItems: 'center',
                                    justifyContent: 'center',
                                    bgcolor: (theme) => alpha(theme.palette.primary.main, 0.1),
                                    color: 'primary.main',
                                }}
                            >
                                <SportsEsportsRoundedIcon />
                            </Box>
                            <Box>
                                <Typography variant="caption" color="text.secondary" fontWeight={500}>
                                    Matches in Batch
                                </Typography>
                                <Typography variant="h6" fontWeight={700} lineHeight={1.2}>
                                    {stats.total}
                                </Typography>
                            </Box>
                        </Card>
                    </Grid>

                    <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                        <Card variant="outlined" sx={{ p: 1.75, display: 'flex', alignItems: 'center', gap: 1.5 }}>
                            <Box
                                sx={{
                                    p: 1,
                                    borderRadius: 1.5,
                                    display: 'flex',
                                    alignItems: 'center',
                                    justifyContent: 'center',
                                    bgcolor: (theme) => alpha(theme.palette.success.main, 0.1),
                                    color: 'success.main',
                                }}
                            >
                                <LeaderboardRoundedIcon />
                            </Box>
                            <Box>
                                <Typography variant="caption" color="text.secondary" fontWeight={500}>
                                    Win Rate
                                </Typography>
                                <Stack direction="row" spacing={0.75} alignItems="center">
                                    <Typography variant="h6" fontWeight={700} lineHeight={1.2}>
                                        {stats.winRate}%
                                    </Typography>
                                    <Typography variant="caption" color="text.secondary">
                                        ({stats.wins}W - {stats.losses}L)
                                    </Typography>
                                </Stack>
                            </Box>
                        </Card>
                    </Grid>

                    <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                        <Card variant="outlined" sx={{ p: 1.75, display: 'flex', alignItems: 'center', gap: 1.5 }}>
                            <Box
                                sx={{
                                    p: 1,
                                    borderRadius: 1.5,
                                    display: 'flex',
                                    alignItems: 'center',
                                    justifyContent: 'center',
                                    bgcolor: (theme) => alpha(theme.palette.secondary.main, 0.1),
                                    color: 'secondary.main',
                                }}
                            >
                                <HourglassEmptyRoundedIcon />
                            </Box>
                            <Box>
                                <Typography variant="caption" color="text.secondary" fontWeight={500}>
                                    Average Turns
                                </Typography>
                                <Typography variant="h6" fontWeight={700} lineHeight={1.2}>
                                    {stats.avgTurns}
                                </Typography>
                            </Box>
                        </Card>
                    </Grid>

                    <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                        <Card variant="outlined" sx={{ p: 1.75, display: 'flex', alignItems: 'center', gap: 1.5 }}>
                            <Box
                                sx={{
                                    p: 1,
                                    borderRadius: 1.5,
                                    display: 'flex',
                                    alignItems: 'center',
                                    justifyContent: 'center',
                                    bgcolor: (theme) => alpha(theme.palette.warning.main, 0.1),
                                    color: 'warning.main',
                                }}
                            >
                                <EmojiEventsRoundedIcon />
                            </Box>
                            <Box>
                                <Typography variant="caption" color="text.secondary" fontWeight={500}>
                                    Most Played
                                </Typography>
                                <Typography variant="h6" fontWeight={700} lineHeight={1.2} noWrap>
                                    {stats.topCharacter || '—'}{' '}
                                    {stats.charCount > 0 && (
                                        <Typography component="span" variant="caption" color="text.secondary">
                                            ({stats.charCount})
                                        </Typography>
                                    )}
                                </Typography>
                            </Box>
                        </Card>
                    </Grid>
                </Grid>
            )}

            {/* Time Navigation Bar */}
            <Paper variant="outlined" sx={{ mb: 2, p: 1, overflow: 'hidden' }}>
                <HistoryNavButtons
                    items={matches}
                    dateFunction={(m: MatchHistoryEntry) => m.matchTime}
                    date={date}
                    setDate={setDate}
                    isBefore={isBefore}
                    setIsBefore={setIsBefore}
                    disabled={loading}
                    datePicker={true}
                    onChange={() => setPage(0)}
                />
            </Paper>

            {/* Tabs & Search Filter */}
            <Stack
                direction={{ xs: 'column', md: 'row' }}
                justifyContent="space-between"
                alignItems={{ xs: 'stretch', md: 'center' }}
                gap={2}
                sx={{ mb: 2 }}
            >
                <Tabs
                    value={resultFilter}
                    onChange={handleTabChange}
                    variant="scrollable"
                    scrollButtons="auto"
                    sx={{ minHeight: 40 }}
                >
                    <Tab
                        value="all"
                        label={`All (${matches.length})`}
                        sx={{ textTransform: 'none', fontWeight: 600, minHeight: 40 }}
                    />
                    <Tab
                        value="win"
                        icon={<EmojiEventsRoundedIcon sx={{ fontSize: 18 }} />}
                        iconPosition="start"
                        label={`Victories (${stats.wins})`}
                        sx={{ textTransform: 'none', fontWeight: 600, minHeight: 40 }}
                    />
                    <Tab
                        value="loss"
                        icon={<CancelRoundedIcon sx={{ fontSize: 18 }} />}
                        iconPosition="start"
                        label={`Defeats (${stats.losses})`}
                        sx={{ textTransform: 'none', fontWeight: 600, minHeight: 40 }}
                    />
                    {stats.ties > 0 && (
                        <Tab
                            value="tie"
                            label={`Draws (${stats.ties})`}
                            sx={{ textTransform: 'none', fontWeight: 600, minHeight: 40 }}
                        />
                    )}
                </Tabs>
            </Stack>

            {/* Filter Bar */}
            <Paper
                variant="outlined"
                sx={{
                    p: 1.5,
                    mb: 2.5,
                    display: 'flex',
                    flexDirection: { xs: 'column', sm: 'row' },
                    gap: 1.5,
                    alignItems: { xs: 'stretch', sm: 'center' },
                }}
            >
                <TextField
                    size="small"
                    placeholder="Search by character, map, game mode, or match ID..."
                    value={searchQuery}
                    onChange={handleSearchChange}
                    InputProps={{
                        startAdornment: (
                            <InputAdornment position="start">
                                <SearchRoundedIcon fontSize="small" sx={{ color: 'text.secondary' }} />
                            </InputAdornment>
                        ),
                        endAdornment: searchQuery ? (
                            <InputAdornment position="end">
                                <IconButton size="small" onClick={() => setSearchQuery('')}>
                                    <CloseRoundedIcon fontSize="small" />
                                </IconButton>
                            </InputAdornment>
                        ) : null,
                    }}
                    sx={{ flex: 1 }}
                />

                {availableMaps.length > 1 && (
                    <FormControl size="small" sx={{ minWidth: 160 }}>
                        <InputLabel id="map-filter-label">Map</InputLabel>
                        <Select
                            labelId="map-filter-label"
                            id="map-filter"
                            value={mapFilter}
                            label="Map"
                            onChange={handleMapChange}
                        >
                            <MenuItem value="all">All Maps</MenuItem>
                            {availableMaps.map(m => (
                                <MenuItem key={m} value={m}>
                                    {m}
                                </MenuItem>
                            ))}
                        </Select>
                    </FormControl>
                )}

                {hasActiveFilters && (
                    <Button
                        size="small"
                        color="inherit"
                        onClick={clearFilters}
                        startIcon={<CloseRoundedIcon />}
                        sx={{ whiteSpace: 'nowrap' }}
                    >
                        Clear Filters
                    </Button>
                )}
            </Paper>

            {/* Loading */}
            {loading && (
                <Box display="flex" flexDirection="column" justifyContent="center" alignItems="center" minHeight="240px" gap={2}>
                    <CircularProgress size={36} />
                    <Typography variant="body2" color="text.secondary">
                        Loading match history records...
                    </Typography>
                </Box>
            )}

            {/* Empty State */}
            {!loading && filteredMatches.length === 0 && (
                <Card
                    variant="outlined"
                    sx={{
                        p: 4,
                        textAlign: 'center',
                        display: 'flex',
                        flexDirection: 'column',
                        alignItems: 'center',
                        gap: 1.5,
                        backgroundColor: (theme) => alpha(theme.palette.action.hover, 0.2),
                    }}
                >
                    <SportsEsportsRoundedIcon sx={{ fontSize: 48, color: 'text.secondary', opacity: 0.6 }} />
                    <Typography variant="h6" fontWeight={600}>
                        {matches.length === 0 ? "No Match Records Found" : "No Matches Match Filters"}
                    </Typography>
                    <Typography variant="body2" color="text.secondary" sx={{ maxWidth: 450 }}>
                        {matches.length === 0
                            ? "No matches were recorded for this player in the selected time range. Use the time controls above to browse older or newer epochs."
                            : "No matches in this batch matched your search and filter criteria."}
                    </Typography>
                    {hasActiveFilters && (
                        <Button size="small" variant="outlined" onClick={clearFilters} sx={{ mt: 1 }}>
                            Reset All Filters
                        </Button>
                    )}
                </Card>
            )}

            {/* Table */}
            {!loading && filteredMatches.length > 0 && (
                <Paper variant="outlined" sx={{ overflow: 'hidden' }}>
                    <TableContainer sx={{ maxHeight: 650 }}>
                        <Table size="small" stickyHeader>
                            <TableHead>
                                <TableRow>
                                    <TableCell sx={{ fontWeight: 600, width: 100 }}>Result</TableCell>
                                    <TableCell sx={{ fontWeight: 600, minWidth: 160 }}>Time</TableCell>
                                    <TableCell sx={{ fontWeight: 600, minWidth: 160 }}>Character</TableCell>
                                    <TableCell sx={{ fontWeight: 600, minWidth: 150 }}>Map & Mode</TableCell>
                                    <TableCell sx={{ fontWeight: 600, width: 90, textAlign: 'center' }}>Turns</TableCell>
                                    <TableCell sx={{ fontWeight: 600, width: 110, textAlign: 'center' }}>Score</TableCell>
                                    <TableCell sx={{ fontWeight: 600, width: 120 }}>Match ID</TableCell>
                                    <TableCell sx={{ fontWeight: 600, width: 80, textAlign: 'center' }}>Action</TableCell>
                                </TableRow>
                            </TableHead>
                            <TableBody>
                                {paginatedMatches.map((match) => {
                                    const isTeamA = match.team === Team.TeamA;
                                    const playerScore = (isTeamA ? match.teamAScore : match.teamBScore) ?? 0;
                                    const opponentScore = (isTeamA ? match.teamBScore : match.teamAScore) ?? 0;
                                    const isWin = match.result === PlayerGameResult.Win;
                                    const isLose = match.result === PlayerGameResult.Lose;

                                    return (
                                        <TableRow
                                            key={match.matchId}
                                            hover
                                            onClick={() => navigate(`/account/${accountId}/matches/${match.matchId}`)}
                                            sx={(theme) => ({
                                                cursor: 'pointer',
                                                '&:last-child td, &:last-child th': { border: 0 },
                                                backgroundColor: getRowBg(match.result, theme),
                                                '&:hover': {
                                                    backgroundColor: getRowHoverBg(match.result, theme),
                                                },
                                            })}
                                        >
                                            {/* Result Chip */}
                                            <TableCell>
                                                {renderResultChip(match.result)}
                                            </TableCell>

                                            {/* Time with relative time */}
                                            <TableCell>
                                                <Typography variant="body2" sx={{ fontWeight: 500 }}>
                                                    {formatDate(match.matchTime)}
                                                </Typography>
                                                <Typography variant="caption" color="text.secondary">
                                                    {formatRelativeTime(match.matchTime)}
                                                </Typography>
                                            </TableCell>

                                            {/* Character */}
                                            <TableCell>
                                                <Stack direction="row" spacing={1.25} alignItems="center">
                                                    <CharacterIcon
                                                        characterType={match.character}
                                                        team={match.team ?? Team.TeamA}
                                                        small
                                                        noTooltip
                                                    />
                                                    <Box>
                                                        <Typography variant="body2" sx={{ fontWeight: 600 }}>
                                                            {match.character}
                                                        </Typography>
                                                        <Chip
                                                            label={match.team === Team.TeamA ? "Team A" : "Team B"}
                                                            size="small"
                                                            variant="outlined"
                                                            sx={{ height: 18, fontSize: '0.65rem' }}
                                                        />
                                                    </Box>
                                                </Stack>
                                            </TableCell>

                                            {/* Map & Mode */}
                                            <TableCell>
                                                <Stack direction="row" spacing={0.5} alignItems="center">
                                                    <MapRoundedIcon sx={{ fontSize: 15, color: 'text.secondary' }} />
                                                    <Typography variant="body2" sx={{ fontWeight: 600 }}>
                                                        {match.mapName || 'Default Map'}
                                                    </Typography>
                                                </Stack>
                                                <Typography variant="caption" color="text.secondary">
                                                    {match.gameType} {match.subType?.split('@')[0]}
                                                </Typography>
                                            </TableCell>

                                            {/* Turns */}
                                            <TableCell align="center">
                                                <Chip
                                                    label={`${match.numOfTurns}T`}
                                                    size="small"
                                                    variant="outlined"
                                                    sx={{ fontWeight: 600 }}
                                                />
                                            </TableCell>

                                            {/* Score */}
                                            <TableCell align="center">
                                                <Typography variant="body2" sx={{ fontWeight: 700, letterSpacing: '0.04em' }}>
                                                    <Box
                                                        component="span"
                                                        sx={{
                                                            color: isWin ? 'success.main' : isLose ? 'error.main' : 'text.primary',
                                                            fontSize: '0.95rem',
                                                        }}
                                                    >
                                                        {playerScore}
                                                    </Box>
                                                    {' - '}
                                                    <Box
                                                        component="span"
                                                        sx={{
                                                            color: isLose ? 'success.main' : isWin ? 'error.main' : 'text.secondary',
                                                            fontSize: '0.95rem',
                                                        }}
                                                    >
                                                        {opponentScore}
                                                    </Box>
                                                </Typography>
                                            </TableCell>

                                            {/* Match ID */}
                                            <TableCell>
                                                <Tooltip title={`Full Match ID: ${match.matchId}`}>
                                                    <code style={{
                                                        fontSize: '0.775rem',
                                                        fontWeight: 600,
                                                        background: 'rgba(128,128,128,0.12)',
                                                        padding: '2px 6px',
                                                        borderRadius: 4,
                                                    }}>
                                                        #{match.matchId.slice(-6)}
                                                    </code>
                                                </Tooltip>
                                            </TableCell>

                                            {/* Action View */}
                                            <TableCell align="center">
                                                <Tooltip title="View full match details">
                                                    <IconButton
                                                        size="small"
                                                        color="primary"
                                                        onClick={(e) => {
                                                            e.stopPropagation();
                                                            navigate(`/account/${accountId}/matches/${match.matchId}`);
                                                        }}
                                                        sx={{
                                                            border: '1px solid',
                                                            borderColor: 'divider',
                                                            '&:hover': { borderColor: 'primary.main' },
                                                        }}
                                                    >
                                                        <OpenInNewRoundedIcon fontSize="small" />
                                                    </IconButton>
                                                </Tooltip>
                                            </TableCell>
                                        </TableRow>
                                    );
                                })}
                            </TableBody>
                        </Table>
                    </TableContainer>

                    {/* Pagination */}
                    <TablePagination
                        component="div"
                        count={filteredMatches.length}
                        page={page}
                        onPageChange={(_e, newPage) => setPage(newPage)}
                        rowsPerPage={rowsPerPage}
                        onRowsPerPageChange={(e) => {
                            setRowsPerPage(parseInt(e.target.value, 10));
                            setPage(0);
                        }}
                        rowsPerPageOptions={[5, 10, 25, 50]}
                    />
                </Paper>
            )}

            {/* Bottom time nav */}
            {!loading && matches.length > 0 && (
                <Box sx={{ mt: 2, display: 'flex', justifyContent: 'center' }}>
                    <HistoryNavButtons
                        items={matches}
                        dateFunction={(m: MatchHistoryEntry) => m.matchTime}
                        date={date}
                        setDate={setDate}
                        isBefore={isBefore}
                        setIsBefore={setIsBefore}
                        disabled={loading}
                        datePicker={false}
                        onChange={() => setPage(0)}
                    />
                </Box>
            )}
        </Box>
    );
};