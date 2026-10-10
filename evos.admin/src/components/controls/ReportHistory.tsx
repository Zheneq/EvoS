import React, {useCallback, useEffect, useMemo, useState} from 'react';
import {
    alpha,
    Box,
    Button,
    Card,
    Chip,
    CircularProgress,
    FormControl,
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
    FeedbackReason,
    formatDate,
    formatRelativeTime,
    getPlayers,
    getReceivedFeedback,
    getSentFeedback,
    PlayerData,
    UserFeedback,
} from "../../lib/Evos";
import {useAuthHeader} from "react-auth-kit";
import {EvosError, processError} from "../../lib/Error";
import {useNavigate} from "react-router-dom";
import {plainAccountLink, plainMatchLink} from "../generic/BasicComponents";
import ChatRoundedIcon from "@mui/icons-material/ChatRounded";
import FeedbackRoundedIcon from "@mui/icons-material/FeedbackRounded";
import WarningAmberRoundedIcon from "@mui/icons-material/WarningAmberRounded";
import SportsEsportsRoundedIcon from "@mui/icons-material/SportsEsportsRounded";
import SearchRoundedIcon from "@mui/icons-material/SearchRounded";
import CloseRoundedIcon from "@mui/icons-material/CloseRounded";
import RefreshRoundedIcon from "@mui/icons-material/RefreshRounded";

interface ReportHistoryProps {
    accountId: number;
    setError: (error: EvosError) => void;
}

type FilterTab = 'all' | 'received' | 'sent';

export const formatFeedbackReason = (reason: FeedbackReason | string): string => {
    switch (reason) {
        case FeedbackReason.LeavingTheGameAFK:
            return "Leaving Game / AFK";
        case FeedbackReason.UnsportsmanlikeConduct:
            return "Unsportsmanlike";
        case FeedbackReason.VerbalHarassment:
            return "Verbal Harassment";
        case FeedbackReason.HateSpeech:
            return "Hate Speech";
        case FeedbackReason.IntentionallyFeeding:
            return "Intentional Feeding";
        case FeedbackReason.SpammingAdvertising:
            return "Spam / Advertising";
        case FeedbackReason.OffensiveName:
            return "Offensive Name";
        case FeedbackReason.Botting:
            return "Botting / Cheating";
        case FeedbackReason.Bug:
            return "Bug Report";
        case FeedbackReason.Suggestion:
            return "Suggestion";
        case FeedbackReason.Other:
            return "Other";
        case FeedbackReason.None:
            return "None";
        default:
            return reason ? reason.replace(/([A-Z])/g, ' $1').trim() : "Unknown";
    }
};

const getReasonChipColor = (reason: FeedbackReason | string): "error" | "warning" | "info" | "default" => {
    switch (reason) {
        case FeedbackReason.HateSpeech:
        case FeedbackReason.OffensiveName:
        case FeedbackReason.Botting:
            return "error";
        case FeedbackReason.UnsportsmanlikeConduct:
        case FeedbackReason.VerbalHarassment:
        case FeedbackReason.IntentionallyFeeding:
        case FeedbackReason.LeavingTheGameAFK:
            return "warning";
        case FeedbackReason.Bug:
        case FeedbackReason.Suggestion:
            return "info";
        default:
            return "default";
    }
};

export const ReportHistory: React.FC<ReportHistoryProps> = ({accountId, setError}: ReportHistoryProps) => {
    const [receivedFeedback, setReceivedFeedback] = useState<UserFeedback[]>([]);
    const [sentFeedback, setSentFeedback] = useState<UserFeedback[]>([]);
    const [loading, setLoading] = useState(true);
    const [players, setPlayers] = useState<Map<number, PlayerData>>(new Map());
    const [refreshTrigger, setRefreshTrigger] = useState(0);

    // Filters and pagination
    const [filterTab, setFilterTab] = useState<FilterTab>('all');
    const [searchQuery, setSearchQuery] = useState('');
    const [reasonFilter, setReasonFilter] = useState('all');
    const [page, setPage] = useState(0);
    const [rowsPerPage, setRowsPerPage] = useState(10);

    const authHeader = useAuthHeader()();
    const navigate = useNavigate();

    const handleRefresh = useCallback(() => {
        setRefreshTrigger(x => x + 1);
    }, []);

    useEffect(() => {
        if (!accountId || accountId === 0) {
            setLoading(false);
            return;
        }

        setLoading(true);
        const abort = new AbortController();

        Promise.all([
            getReceivedFeedback(abort, authHeader, accountId),
            getSentFeedback(abort, authHeader, accountId),
        ])
            .then(async ([receivedResp, sentResp]) => {
                const received = receivedResp.data.feedback || [];
                const sent = sentResp.data.feedback || [];
                setReceivedFeedback(received);
                setSentFeedback(sent);

                const accountIds = Array.from(new Set([
                    ...received.map(it => it.accountId),
                    ...sent.map(it => it.accountId),
                    ...received.map(it => it.reportedPlayerAccountId),
                    ...sent.map(it => it.reportedPlayerAccountId),
                    accountId,
                ])).filter(id => !!id && id !== 0);

                if (accountIds.length > 0) {
                    try {
                        const playersResp = await getPlayers(abort, authHeader, accountIds);
                        const map = new Map(playersResp.data.players.map(p => [p.accountId, p]));
                        setPlayers(map);
                    } catch (e) {
                        if (!abort.signal.aborted) {
                            processError(e, setError, navigate);
                        }
                    }
                }
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
    }, [accountId, authHeader, navigate, setError, refreshTrigger]);

    const allFeedback = useMemo(() => {
        return [...receivedFeedback, ...sentFeedback].sort(
            (a, b) => new Date(b.time).getTime() - new Date(a.time).getTime()
        );
    }, [receivedFeedback, sentFeedback]);

    const availableReasons = useMemo(() => {
        const set = new Set<string>();
        allFeedback.forEach(f => {
            if (f.reason) set.add(f.reason);
        });
        return Array.from(set).sort();
    }, [allFeedback]);

    const filteredFeedback = useMemo(() => {
        return allFeedback.filter((item) => {
            const isReceived = item.reportedPlayerAccountId === accountId;
            if (filterTab === 'received' && !isReceived) return false;
            if (filterTab === 'sent' && isReceived) return false;

            if (reasonFilter !== 'all' && item.reason !== reasonFilter) {
                return false;
            }

            if (searchQuery.trim()) {
                const q = searchQuery.toLowerCase().trim();
                const fromHandle = (players.get(item.accountId)?.handle || "").toLowerCase();
                const toHandle = (item.reportedPlayerHandle || players.get(item.reportedPlayerAccountId)?.handle || "").toLowerCase();
                const message = (item.message || "").toLowerCase();
                const reason = (item.reason || "").toLowerCase();
                const context = (item.context || "").toLowerCase();

                if (
                    !fromHandle.includes(q) &&
                    !toHandle.includes(q) &&
                    !message.includes(q) &&
                    !reason.includes(q) &&
                    !context.includes(q)
                ) {
                    return false;
                }
            }

            return true;
        });
    }, [allFeedback, filterTab, reasonFilter, searchQuery, accountId, players]);

    const paginatedFeedback = useMemo(() => {
        return filteredFeedback.slice(page * rowsPerPage, page * rowsPerPage + rowsPerPage);
    }, [filteredFeedback, page, rowsPerPage]);

    const getChatLink = (msg: UserFeedback) => {
        const ts = Math.floor(new Date(msg.time).getTime() / 1000);
        return `/account/${msg.reportedPlayerAccountId}/chat?before=false&ts=${ts - 300}&limit=${ts + 300}`;
    };

    const handleTabChange = (_event: React.SyntheticEvent, newValue: FilterTab) => {
        setFilterTab(newValue);
        setPage(0);
    };

    const handleSearchChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        setSearchQuery(e.target.value);
        setPage(0);
    };

    const handleReasonChange = (e: SelectChangeEvent) => {
        setReasonFilter(e.target.value);
        setPage(0);
    };

    const hasActiveFilters = searchQuery.trim() !== '' || reasonFilter !== 'all' || filterTab !== 'all';

    const clearFilters = () => {
        setSearchQuery('');
        setReasonFilter('all');
        setFilterTab('all');
        setPage(0);
    };

    return (
        <Box sx={{ width: '100%', p: 2 }}>
            {/* Top Toolbar: Tabs and Quick Counts */}
            <Stack
                direction={{ xs: 'column', md: 'row' }}
                justifyContent="space-between"
                alignItems={{ xs: 'stretch', md: 'center' }}
                gap={2}
                sx={{ mb: 2 }}
            >
                <Tabs
                    value={filterTab}
                    onChange={handleTabChange}
                    variant="scrollable"
                    scrollButtons="auto"
                    sx={{ minHeight: 40 }}
                >
                    <Tab
                        value="all"
                        label={`All (${allFeedback.length})`}
                        sx={{ textTransform: 'none', fontWeight: 600, minHeight: 40 }}
                    />
                    <Tab
                        value="received"
                        icon={<WarningAmberRoundedIcon sx={{ fontSize: 18 }} />}
                        iconPosition="start"
                        label={`Received (${receivedFeedback.length})`}
                        sx={{ textTransform: 'none', fontWeight: 600, minHeight: 40 }}
                    />
                    <Tab
                        value="sent"
                        icon={<FeedbackRoundedIcon sx={{ fontSize: 18 }} />}
                        iconPosition="start"
                        label={`Sent (${sentFeedback.length})`}
                        sx={{ textTransform: 'none', fontWeight: 600, minHeight: 40 }}
                    />
                </Tabs>

                {/* Right toolbar controls */}
                <Stack direction="row" spacing={1} alignItems="center">
                    <Tooltip title="Refresh feedback data">
                        <span>
                            <IconButton
                                size="small"
                                onClick={handleRefresh}
                                disabled={loading}
                                sx={{ border: '1px solid', borderColor: 'divider' }}
                            >
                                <RefreshRoundedIcon fontSize="small" />
                            </IconButton>
                        </span>
                    </Tooltip>
                </Stack>
            </Stack>

            {/* Filter Bar: Search & Reason Dropdown */}
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
                    placeholder="Search by player, reason, message, or match ID..."
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

                <FormControl size="small" sx={{ minWidth: 180 }}>
                    <InputLabel id="reason-filter-label">Reason</InputLabel>
                    <Select
                        labelId="reason-filter-label"
                        id="reason-filter"
                        value={reasonFilter}
                        label="Reason"
                        onChange={handleReasonChange}
                    >
                        <MenuItem value="all">All Reasons</MenuItem>
                        {availableReasons.map(reason => (
                            <MenuItem key={reason} value={reason}>
                                {formatFeedbackReason(reason)}
                            </MenuItem>
                        ))}
                    </Select>
                </FormControl>

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

            {/* Loading Indicator */}
            {loading && (
                <Box display="flex" flexDirection="column" justifyContent="center" alignItems="center" minHeight="240px" gap={2}>
                    <CircularProgress size={36} />
                    <Typography variant="body2" color="text.secondary">
                        Loading reports and player profiles...
                    </Typography>
                </Box>
            )}

            {/* Empty State */}
            {!loading && filteredFeedback.length === 0 && (
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
                    <FeedbackRoundedIcon sx={{ fontSize: 48, color: 'text.secondary', opacity: 0.6 }} />
                    <Typography variant="h6" fontWeight={600}>
                        {allFeedback.length === 0 ? "No Reports or Feedback on Record" : "No Matching Reports"}
                    </Typography>
                    <Typography variant="body2" color="text.secondary" sx={{ maxWidth: 450 }}>
                        {allFeedback.length === 0
                            ? "This account has not received any misconduct reports and has not submitted any player feedback."
                            : "No reports matched your current search and filter criteria."}
                    </Typography>
                    {hasActiveFilters && (
                        <Button size="small" variant="outlined" onClick={clearFilters} sx={{ mt: 1 }}>
                            Reset All Filters
                        </Button>
                    )}
                </Card>
            )}

            {/* Table */}
            {!loading && filteredFeedback.length > 0 && (
                <Paper variant="outlined" sx={{ overflow: 'hidden' }}>
                    <TableContainer sx={{ maxHeight: 650 }}>
                        <Table size="small" stickyHeader>
                            <TableHead>
                                <TableRow>
                                    <TableCell sx={{ fontWeight: 600, width: 110 }}>Type</TableCell>
                                    <TableCell sx={{ fontWeight: 600, minWidth: 160 }}>Time</TableCell>
                                    <TableCell sx={{ fontWeight: 600, minWidth: 150 }}>Reason</TableCell>
                                    <TableCell sx={{ fontWeight: 600, minWidth: 240 }}>Message</TableCell>
                                    <TableCell sx={{ fontWeight: 600, minWidth: 140 }}>Reported (To)</TableCell>
                                    <TableCell sx={{ fontWeight: 600, minWidth: 140 }}>Reporter (From)</TableCell>
                                    <TableCell sx={{ fontWeight: 600, width: 120 }}>Context</TableCell>
                                    <TableCell sx={{ fontWeight: 600, width: 80, textAlign: 'center' }}>Chat</TableCell>
                                </TableRow>
                            </TableHead>
                            <TableBody>
                                {paginatedFeedback.map((msg, idx) => {
                                    const isReceived = msg.reportedPlayerAccountId === accountId;
                                    const rowKey = `${msg.time}-${msg.accountId}-${msg.reportedPlayerAccountId}-${idx}`;
                                    const reportedHandle = msg.reportedPlayerHandle || players.get(msg.reportedPlayerAccountId)?.handle || "UNKNWN";
                                    const reporterHandle = players.get(msg.accountId)?.handle ?? (msg.accountId === accountId ? "This Account" : "UNKNWN");

                                    return (
                                        <TableRow
                                            key={rowKey}
                                            hover
                                            sx={(theme) => ({
                                                '&:last-child td, &:last-child th': { border: 0 },
                                                backgroundColor: isReceived
                                                    ? alpha(theme.palette.error.main, 0.05)
                                                    : alpha(theme.palette.info.main, 0.04),
                                                '&:hover': {
                                                    backgroundColor: isReceived
                                                        ? `${alpha(theme.palette.error.main, 0.12)} !important`
                                                        : `${alpha(theme.palette.info.main, 0.10)} !important`,
                                                },
                                            })}
                                        >
                                            {/* Type Chip */}
                                            <TableCell>
                                                {isReceived ? (
                                                    <Chip
                                                        label="Received"
                                                        size="small"
                                                        color="error"
                                                        variant="outlined"
                                                        icon={<WarningAmberRoundedIcon sx={{ fontSize: '14px !important' }} />}
                                                        sx={{ fontWeight: 600, fontSize: '0.75rem' }}
                                                    />
                                                ) : (
                                                    <Chip
                                                        label="Sent"
                                                        size="small"
                                                        color="info"
                                                        variant="outlined"
                                                        icon={<FeedbackRoundedIcon sx={{ fontSize: '14px !important' }} />}
                                                        sx={{ fontWeight: 600, fontSize: '0.75rem' }}
                                                    />
                                                )}
                                            </TableCell>

                                            {/* Time with relative time */}
                                            <TableCell>
                                                <Typography variant="body2" sx={{ fontWeight: 500 }}>
                                                    {formatDate(msg.time)}
                                                </Typography>
                                                <Typography variant="caption" color="text.secondary">
                                                    {formatRelativeTime(msg.time)}
                                                </Typography>
                                            </TableCell>

                                            {/* Reason Chip */}
                                            <TableCell>
                                                <Chip
                                                    label={formatFeedbackReason(msg.reason)}
                                                    size="small"
                                                    color={getReasonChipColor(msg.reason)}
                                                    sx={{ fontWeight: 500 }}
                                                />
                                            </TableCell>

                                            {/* Message */}
                                            <TableCell>
                                                {msg.message ? (
                                                    <Typography
                                                        variant="body2"
                                                        sx={{
                                                            whiteSpace: 'pre-wrap',
                                                            wordBreak: 'break-word',
                                                            maxWidth: 360,
                                                        }}
                                                    >
                                                        {msg.message}
                                                    </Typography>
                                                ) : (
                                                    <Typography variant="caption" sx={{ fontStyle: 'italic', color: 'text.disabled' }}>
                                                        (No message provided)
                                                    </Typography>
                                                )}
                                            </TableCell>

                                            {/* Reported Player (To) */}
                                            <TableCell>
                                                <Stack direction="row" spacing={0.75} alignItems="center">
                                                    {plainAccountLink(msg.reportedPlayerAccountId, reportedHandle, navigate)}
                                                    {msg.reportedPlayerAccountId === accountId && (
                                                        <Chip
                                                            label="Offender"
                                                            size="small"
                                                            color="error"
                                                            sx={{ height: 18, fontSize: '0.65rem', fontWeight: 600 }}
                                                        />
                                                    )}
                                                </Stack>
                                            </TableCell>

                                            {/* Reporter (From) */}
                                            <TableCell>
                                                <Stack direction="row" spacing={0.75} alignItems="center">
                                                    {plainAccountLink(msg.accountId, reporterHandle, navigate)}
                                                    {msg.accountId === accountId && (
                                                        <Chip
                                                            label="Reporter"
                                                            size="small"
                                                            color="info"
                                                            sx={{ height: 18, fontSize: '0.65rem', fontWeight: 600 }}
                                                        />
                                                    )}
                                                </Stack>
                                            </TableCell>

                                            {/* Context Match */}
                                            <TableCell>
                                                {msg.context ? (
                                                    <Stack direction="row" spacing={0.5} alignItems="center">
                                                        <SportsEsportsRoundedIcon fontSize="small" sx={{ color: 'text.secondary' }} />
                                                        {plainMatchLink(accountId, msg.context, navigate, `Game #${msg.context.slice(-5)}`)}
                                                    </Stack>
                                                ) : (
                                                    <Typography variant="body2" color="text.disabled">—</Typography>
                                                )}
                                            </TableCell>

                                            {/* Chat Link */}
                                            <TableCell align="center">
                                                <Tooltip title="View surrounding chat logs (±5 mins)">
                                                    <IconButton
                                                        size="small"
                                                        color="primary"
                                                        component="a"
                                                        href={getChatLink(msg)}
                                                        target="_blank"
                                                        rel="noopener noreferrer"
                                                        sx={{
                                                            border: '1px solid',
                                                            borderColor: 'divider',
                                                            '&:hover': { borderColor: 'primary.main' },
                                                        }}
                                                    >
                                                        <ChatRoundedIcon fontSize="small" />
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
                        count={filteredFeedback.length}
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
        </Box>
    );
};