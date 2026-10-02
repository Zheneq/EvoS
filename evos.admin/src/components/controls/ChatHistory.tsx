import React, {useEffect, useMemo, useState} from 'react';
import {
    alpha,
    Box,
    Button,
    Card,
    Chip,
    CircularProgress,
    FormControl,
    FormControlLabel,
    Grid,
    IconButton,
    InputAdornment,
    InputLabel,
    MenuItem,
    Paper,
    Select,
    SelectChangeEvent,
    Stack,
    Switch,
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
    ChatMessage,
    ChatType,
    formatDate,
    formatRelativeTime,
    getChatHistory,
    getPlayers,
    PlayerData,
} from "../../lib/Evos";
import {useAuthHeader} from "react-auth-kit";
import {EvosError, processError} from "../../lib/Error";
import {useNavigate, useSearchParams} from "react-router-dom";
import ErrorDialog from "../generic/ErrorDialog";
import {plainAccountLink, plainMatchLink} from "../generic/BasicComponents";
import {CharacterIcon} from "../atlas/CharacterIcon";
import HistoryNavButtons from "../generic/HistoryNavButtons";
import {useBeforeParamState, useDateParamState} from "../../lib/Lib";
import dayjs from "dayjs";
import ChatRoundedIcon from "@mui/icons-material/ChatRounded";
import SearchRoundedIcon from "@mui/icons-material/SearchRounded";
import CloseRoundedIcon from "@mui/icons-material/CloseRounded";
import SportsEsportsRoundedIcon from "@mui/icons-material/SportsEsportsRounded";
import VolumeOffRoundedIcon from "@mui/icons-material/VolumeOffRounded";
import ForumRoundedIcon from "@mui/icons-material/ForumRounded";
import LockRoundedIcon from "@mui/icons-material/LockRounded";
import ContentCopyRoundedIcon from "@mui/icons-material/ContentCopyRounded";

interface ChatHistoryProps {
    accountId: number;
}

type ChatFilterTab = 'all' | 'sent' | 'received' | 'whispers';

const LIMIT = 50;

export const formatChatType = (type: ChatType | string): string => {
    switch (type) {
        case ChatType.GlobalChat:
            return "Global";
        case ChatType.GameChat:
            return "Game";
        case ChatType.TeamChat:
            return "Team";
        case ChatType.GroupChat:
            return "Group";
        case ChatType.WhisperChat:
            return "Whisper";
        case ChatType.CombatLog:
            return "Combat";
        case ChatType.SystemMessage:
            return "System";
        case ChatType.BroadcastMessage:
            return "Broadcast";
        case ChatType.PingChat:
            return "Ping";
        case ChatType.ScriptedChat:
            return "Script";
        case ChatType.Error:
        case ChatType.Exception:
            return "Error";
        default:
            return type ? type.replace(/Chat$/, '') : "Chat";
    }
};

const getChatTypeColor = (type: ChatType): "default" | "primary" | "secondary" | "info" | "success" | "warning" | "error" => {
    switch (type) {
        case ChatType.GlobalChat:
            return "default";
        case ChatType.GameChat:
            return "primary";
        case ChatType.TeamChat:
            return "info";
        case ChatType.GroupChat:
            return "success";
        case ChatType.WhisperChat:
            return "secondary";
        case ChatType.CombatLog:
        case ChatType.SystemMessage:
        case ChatType.BroadcastMessage:
            return "warning";
        case ChatType.Error:
        case ChatType.Exception:
            return "error";
        default:
            return "default";
    }
};

export const ChatHistory: React.FC<ChatHistoryProps> = ({accountId}: ChatHistoryProps) => {
    const [searchParams, setSearchParams] = useSearchParams();
    const [messages, setMessages] = useState<ChatMessage[]>([]);
    const [players, setPlayers] = useState<Map<number, PlayerData>>(new Map());
    const [loading, setLoading] = useState(true);

    const [date, setDate] = useDateParamState(searchParams);
    const [isBefore, setIsBefore] = useBeforeParamState(searchParams);

    const [isWithGeneralChat, setIsWithGeneralChat] = useState(() => {
        const generalChatParam = searchParams.get('generalChat');
        return generalChatParam === null ? true : generalChatParam === 'true';
    });

    // Filtering & pagination
    const [filterTab, setFilterTab] = useState<ChatFilterTab>('all');
    const [typeFilter, setTypeFilter] = useState('all');
    const [searchQuery, setSearchQuery] = useState('');
    const [page, setPage] = useState(0);
    const [rowsPerPage, setRowsPerPage] = useState(10);
    const [copiedIndex, setCopiedIndex] = useState<number | null>(null);

    const [error, setError] = useState<EvosError>();
    const authHeader = useAuthHeader()();
    const navigate = useNavigate();

    const handleGeneralChatChange = (event: React.ChangeEvent<HTMLInputElement>) => {
        const newValue = event.target.checked;
        setIsWithGeneralChat(newValue);
    };

    useEffect(() => {
        const newParams = new URLSearchParams(searchParams);
        newParams.set('before', isBefore.toString());
        newParams.set('generalChat', isWithGeneralChat.toString());
        newParams.set('ts', Math.floor(date.unix()).toString());
        setSearchParams(newParams);
        // eslint-disable-next-line
    }, [date, isBefore, isWithGeneralChat]);

    const limit = searchParams.get('limit');

    useEffect(() => {
        if (accountId === 0) {
            setLoading(false);
            return;
        }

        setLoading(true);
        const abort = new AbortController();
        const timestamp = Math.floor(date.unix());
        const limitTs = limit ? dayjs(parseInt(limit) * 1000) : undefined;

        getChatHistory(abort, authHeader, accountId, timestamp, isBefore, true, isWithGeneralChat, LIMIT)
            .then((resp) => {
                const respMessages = limitTs
                    ? isBefore
                        ? resp.data.messages.filter(m => dayjs(m.time).isAfter(limitTs))
                        : resp.data.messages.filter(m => dayjs(m.time).isBefore(limitTs))
                    : resp.data.messages;
                setMessages(respMessages || []);
                setPage(0);

                const accountIds = Array.from(
                    new Set([
                        ...resp.data.messages.map(msg => msg.senderId),
                        ...resp.data.messages.flatMap(msg => msg.recipients),
                        ...resp.data.messages.flatMap(msg => msg.blockedRecipients || []),
                    ])
                ).filter(id => !!id && id !== 0);

                if (accountIds.length === 0) {
                    return Promise.resolve(undefined);
                }
                return getPlayers(abort, authHeader, accountIds);
            })
            .then((playersResp) => {
                const playersMap = playersResp
                    ? new Map(playersResp.data.players.map(player => [player.accountId, player]))
                    : new Map();
                setPlayers(playersMap);
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
    }, [accountId, authHeader, date, isBefore, isWithGeneralChat, limit, navigate]);

    // Metrics for active batch
    const stats = useMemo(() => {
        if (!messages || messages.length === 0) {
            return { total: 0, sent: 0, received: 0, whispers: 0, muted: 0 };
        }
        const total = messages.length;
        const sent = messages.filter(m => m.senderId === accountId).length;
        const received = total - sent;
        const whispers = messages.filter(m => m.type === ChatType.WhisperChat).length;
        const muted = messages.filter(m => m.isMuted).length;

        return { total, sent, received, whispers, muted };
    }, [messages, accountId]);

    const availableTypes = useMemo(() => {
        const set = new Set<string>();
        messages.forEach(m => {
            if (m.type) set.add(m.type);
        });
        return Array.from(set).sort();
    }, [messages]);

    const filteredMessages = useMemo(() => {
        return messages.filter(msg => {
            const isSent = msg.senderId === accountId;
            if (filterTab === 'sent' && !isSent) return false;
            if (filterTab === 'received' && isSent) return false;
            if (filterTab === 'whispers' && msg.type !== ChatType.WhisperChat) return false;

            if (typeFilter !== 'all' && msg.type !== typeFilter) return false;

            if (searchQuery.trim()) {
                const q = searchQuery.toLowerCase().trim();
                const sender = (msg.senderHandle || players.get(msg.senderId)?.handle || '').toLowerCase();
                const text = (msg.message || '').toLowerCase();
                const type = (msg.type || '').toLowerCase();
                const game = (msg.game || '').toLowerCase();
                const hasRecipientMatch = msg.recipients?.some(id =>
                    (players.get(id)?.handle || '').toLowerCase().includes(q)
                );

                if (
                    !sender.includes(q) &&
                    !text.includes(q) &&
                    !type.includes(q) &&
                    !game.includes(q) &&
                    !hasRecipientMatch
                ) {
                    return false;
                }
            }

            return true;
        });
    }, [messages, filterTab, typeFilter, searchQuery, accountId, players]);

    const paginatedMessages = useMemo(() => {
        return filteredMessages.slice(page * rowsPerPage, page * rowsPerPage + rowsPerPage);
    }, [filteredMessages, page, rowsPerPage]);

    const handleTabChange = (_event: React.SyntheticEvent, newValue: ChatFilterTab) => {
        setFilterTab(newValue);
        setPage(0);
    };

    const handleTypeChange = (e: SelectChangeEvent) => {
        setTypeFilter(e.target.value);
        setPage(0);
    };

    const handleSearchChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        setSearchQuery(e.target.value);
        setPage(0);
    };

    const hasActiveFilters = searchQuery.trim() !== '' || typeFilter !== 'all' || filterTab !== 'all';

    const clearFilters = () => {
        setSearchQuery('');
        setTypeFilter('all');
        setFilterTab('all');
        setPage(0);
    };

    const copyMessage = (text: string, index: number) => {
        navigator.clipboard.writeText(text).then(() => {
            setCopiedIndex(index);
            setTimeout(() => setCopiedIndex(null), 1500);
        });
    };

    const getRowBg = (msg: ChatMessage, theme: any) => {
        const isMe = msg.senderId === accountId;
        if (msg.type === ChatType.WhisperChat) {
            return alpha(theme.palette.secondary.main, 0.05);
        }
        if (msg.type === ChatType.TeamChat) {
            return alpha(theme.palette.info.main, 0.05);
        }
        if (msg.type === ChatType.GameChat) {
            return alpha(theme.palette.primary.main, 0.04);
        }
        if (msg.type === ChatType.GroupChat) {
            return alpha(theme.palette.success.main, 0.05);
        }
        if (isMe) {
            return alpha(theme.palette.primary.main, 0.03);
        }
        return 'transparent';
    };

    const getRowHoverBg = (msg: ChatMessage, theme: any) => {
        if (msg.type === ChatType.WhisperChat) {
            return `${alpha(theme.palette.secondary.main, 0.12)} !important`;
        }
        if (msg.type === ChatType.TeamChat) {
            return `${alpha(theme.palette.info.main, 0.12)} !important`;
        }
        if (msg.type === ChatType.GameChat) {
            return `${alpha(theme.palette.primary.main, 0.10)} !important`;
        }
        return `${alpha(theme.palette.action.hover, 0.08)} !important`;
    };

    const renderRecipients = (msg: ChatMessage) => {
        if ((!msg.recipients || msg.recipients.length === 0) && (!msg.blockedRecipients || msg.blockedRecipients.length === 0)) {
            return (
                <Typography variant="caption" color="text.secondary" sx={{ fontStyle: 'italic' }}>
                    {msg.type === ChatType.GlobalChat ? "Global Channel" : msg.type === ChatType.GameChat ? "All in Match" : "Broadcast"}
                </Typography>
            );
        }

        return (
            <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5, alignItems: 'center' }}>
                {msg.recipients.map((id) => (
                    <Chip
                        key={`recip-${id}`}
                        label={players.get(id)?.handle ?? `ID: ${id}`}
                        size="small"
                        variant="outlined"
                        clickable
                        onClick={() => navigate(`/account/${id}`)}
                        sx={{ height: 22, fontSize: '0.725rem' }}
                    />
                ))}
                {(msg.blockedRecipients || []).map((id) => (
                    <Tooltip key={`blocked-${id}`} title="Recipient has blocked this message">
                        <Chip
                            label={players.get(id)?.handle ?? `ID: ${id}`}
                            size="small"
                            color="error"
                            variant="outlined"
                            icon={<VolumeOffRoundedIcon sx={{ fontSize: '12px !important' }} />}
                            clickable
                            onClick={() => navigate(`/account/${id}`)}
                            sx={{ height: 22, fontSize: '0.725rem', textDecoration: 'line-through' }}
                        />
                    </Tooltip>
                ))}
            </Box>
        );
    };

    return (
        <Box sx={{ width: '100%', p: 2 }}>
            {error && <ErrorDialog error={error} onDismiss={() => setError(undefined)} />}

            {/* KPI Performance Cards */}
            {!loading && messages.length > 0 && (
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
                                <ChatRoundedIcon />
                            </Box>
                            <Box>
                                <Typography variant="caption" color="text.secondary" fontWeight={500}>
                                    Total in Batch
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
                                <ForumRoundedIcon />
                            </Box>
                            <Box>
                                <Typography variant="caption" color="text.secondary" fontWeight={500}>
                                    Messages Sent
                                </Typography>
                                <Typography variant="h6" fontWeight={700} lineHeight={1.2}>
                                    {stats.sent}{' '}
                                    <Typography component="span" variant="caption" color="text.secondary">
                                        ({Math.round((stats.sent / stats.total) * 100)}%)
                                    </Typography>
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
                                    bgcolor: (theme) => alpha(theme.palette.secondary.main, 0.1),
                                    color: 'secondary.main',
                                }}
                            >
                                <LockRoundedIcon />
                            </Box>
                            <Box>
                                <Typography variant="caption" color="text.secondary" fontWeight={500}>
                                    Whispers
                                </Typography>
                                <Typography variant="h6" fontWeight={700} lineHeight={1.2}>
                                    {stats.whispers}
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
                                    bgcolor: (theme) => alpha(stats.muted > 0 ? theme.palette.error.main : theme.palette.info.main, 0.1),
                                    color: stats.muted > 0 ? 'error.main' : 'info.main',
                                }}
                            >
                                <VolumeOffRoundedIcon />
                            </Box>
                            <Box>
                                <Typography variant="caption" color="text.secondary" fontWeight={500}>
                                    Muted Messages
                                </Typography>
                                <Typography variant="h6" fontWeight={700} lineHeight={1.2} color={stats.muted > 0 ? 'error.main' : 'inherit'}>
                                    {stats.muted}
                                </Typography>
                            </Box>
                        </Card>
                    </Grid>
                </Grid>
            )}

            {/* Time Navigation & General Chat Toggle */}
            <Paper variant="outlined" sx={{ mb: 2, p: 1.5 }}>
                <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" alignItems="center" gap={1.5} sx={{ mb: 1 }}>
                    <FormControlLabel
                        control={
                            <Switch
                                checked={isWithGeneralChat}
                                onChange={handleGeneralChatChange}
                                size="small"
                            />
                        }
                        label={
                            <Typography variant="body2" fontWeight={500}>
                                Include general chat
                            </Typography>
                        }
                    />
                </Stack>

                <HistoryNavButtons
                    items={messages}
                    dateFunction={(m: ChatMessage) => m.time}
                    date={date}
                    setDate={setDate}
                    isBefore={isBefore}
                    setIsBefore={setIsBefore}
                    disabled={loading}
                    datePicker={true}
                    onChange={() => {
                        const newParams = new URLSearchParams(searchParams);
                        newParams.delete('limit');
                        setSearchParams(newParams);
                        setPage(0);
                    }}
                />
            </Paper>

            {/* Filter Tabs */}
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
                        label={`All (${messages.length})`}
                        sx={{ textTransform: 'none', fontWeight: 600, minHeight: 40 }}
                    />
                    <Tab
                        value="sent"
                        label={`Sent (${stats.sent})`}
                        sx={{ textTransform: 'none', fontWeight: 600, minHeight: 40 }}
                    />
                    <Tab
                        value="received"
                        label={`Received (${stats.received})`}
                        sx={{ textTransform: 'none', fontWeight: 600, minHeight: 40 }}
                    />
                    {stats.whispers > 0 && (
                        <Tab
                            value="whispers"
                            label={`Whispers (${stats.whispers})`}
                            sx={{ textTransform: 'none', fontWeight: 600, minHeight: 40 }}
                        />
                    )}
                </Tabs>
            </Stack>

            {/* Filter Search Bar */}
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
                    placeholder="Search by sender, recipient, message text, or game ID..."
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

                {availableTypes.length > 1 && (
                    <FormControl size="small" sx={{ minWidth: 160 }}>
                        <InputLabel id="channel-type-filter-label">Channel</InputLabel>
                        <Select
                            labelId="channel-type-filter-label"
                            id="channel-type-filter"
                            value={typeFilter}
                            label="Channel"
                            onChange={handleTypeChange}
                        >
                            <MenuItem value="all">All Channels</MenuItem>
                            {availableTypes.map(t => (
                                <MenuItem key={t} value={t}>
                                    {formatChatType(t)}
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
                        Loading chat history messages...
                    </Typography>
                </Box>
            )}

            {/* Empty State */}
            {!loading && filteredMessages.length === 0 && (
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
                    <ChatRoundedIcon sx={{ fontSize: 48, color: 'text.secondary', opacity: 0.6 }} />
                    <Typography variant="h6" fontWeight={600}>
                        {messages.length === 0 ? "No Chat Messages Found" : "No Messages Match Filters"}
                    </Typography>
                    <Typography variant="body2" color="text.secondary" sx={{ maxWidth: 450 }}>
                        {messages.length === 0
                            ? "No chat messages were recorded around this time window. Use the date and navigation controls above to search other time periods."
                            : "No messages in the loaded batch matched your current search and filter criteria."}
                    </Typography>
                    {hasActiveFilters && (
                        <Button size="small" variant="outlined" onClick={clearFilters} sx={{ mt: 1 }}>
                            Reset All Filters
                        </Button>
                    )}
                </Card>
            )}

            {/* Table */}
            {!loading && filteredMessages.length > 0 && (
                <Paper variant="outlined" sx={{ overflow: 'hidden' }}>
                    <TableContainer sx={{ maxHeight: 680 }}>
                        <Table size="small" stickyHeader>
                            <TableHead>
                                <TableRow>
                                    <TableCell sx={{ fontWeight: 600, width: 100 }}>Channel</TableCell>
                                    <TableCell sx={{ fontWeight: 600, minWidth: 160 }}>Time</TableCell>
                                    <TableCell sx={{ fontWeight: 600, minWidth: 150 }}>Sender (From)</TableCell>
                                    <TableCell sx={{ fontWeight: 600, minWidth: 280 }}>Message</TableCell>
                                    <TableCell sx={{ fontWeight: 600, minWidth: 180 }}>Recipients (To)</TableCell>
                                    <TableCell sx={{ fontWeight: 600, minWidth: 130 }}>Character</TableCell>
                                    <TableCell sx={{ fontWeight: 600, width: 120 }}>Game</TableCell>
                                    <TableCell sx={{ fontWeight: 600, width: 60, textAlign: 'center' }}>Copy</TableCell>
                                </TableRow>
                            </TableHead>
                            <TableBody>
                                {paginatedMessages.map((msg, index) => {
                                    const isMe = msg.senderId === accountId;
                                    const isBlockedForMe = msg.blockedRecipients && msg.blockedRecipients.includes(accountId);
                                    const rowKey = `${msg.time}-${msg.senderId}-${index}`;

                                    return (
                                        <TableRow
                                            key={rowKey}
                                            hover
                                            sx={(theme) => ({
                                                '&:last-child td, &:last-child th': { border: 0 },
                                                backgroundColor: getRowBg(msg, theme),
                                                '&:hover': {
                                                    backgroundColor: getRowHoverBg(msg, theme),
                                                },
                                            })}
                                        >
                                            {/* Channel Chip */}
                                            <TableCell>
                                                <Chip
                                                    label={formatChatType(msg.type)}
                                                    size="small"
                                                    color={getChatTypeColor(msg.type)}
                                                    variant="outlined"
                                                    sx={{ fontWeight: 600, fontSize: '0.725rem' }}
                                                />
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

                                            {/* Sender (From) */}
                                            <TableCell>
                                                <Stack direction="row" spacing={0.75} alignItems="center">
                                                    {plainAccountLink(
                                                        msg.senderId,
                                                        msg.senderHandle || players.get(msg.senderId)?.handle || `ID: ${msg.senderId}`,
                                                        navigate,
                                                        msg.isMuted ? { textDecoration: 'line-through' } : undefined
                                                    )}
                                                    {isMe && (
                                                        <Chip
                                                            label="You"
                                                            size="small"
                                                            color="primary"
                                                            variant="outlined"
                                                            sx={{ height: 18, fontSize: '0.65rem', fontWeight: 700 }}
                                                        />
                                                    )}
                                                </Stack>
                                            </TableCell>

                                            {/* Message */}
                                            <TableCell>
                                                <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.25 }}>
                                                    <Typography
                                                        variant="body2"
                                                        sx={{
                                                            whiteSpace: 'pre-wrap',
                                                            wordBreak: 'break-word',
                                                            textDecoration: (msg.isMuted || isBlockedForMe) ? 'line-through' : 'none',
                                                            opacity: (msg.isMuted || isBlockedForMe) ? 0.65 : 1,
                                                            fontWeight: isMe ? 500 : 400,
                                                        }}
                                                    >
                                                        {msg.message}
                                                    </Typography>
                                                    {msg.isMuted && (
                                                        <Stack direction="row" spacing={0.5} alignItems="center">
                                                            <VolumeOffRoundedIcon sx={{ fontSize: 13, color: 'error.main' }} />
                                                            <Typography variant="caption" color="error.main" fontWeight={600}>
                                                                Muted by system
                                                            </Typography>
                                                        </Stack>
                                                    )}
                                                </Box>
                                            </TableCell>

                                            {/* Recipients (To) */}
                                            <TableCell>
                                                {renderRecipients(msg)}
                                            </TableCell>

                                            {/* Character */}
                                            <TableCell>
                                                {msg.game && msg.character && msg.character !== CharacterType.None ? (
                                                    <Stack direction="row" spacing={1} alignItems="center">
                                                        <CharacterIcon
                                                            characterType={msg.character}
                                                            team={msg.team}
                                                            small
                                                            noTooltip
                                                        />
                                                        <Typography variant="caption" fontWeight={600}>
                                                            {msg.character}
                                                        </Typography>
                                                    </Stack>
                                                ) : (
                                                    <Typography variant="caption" color="text.disabled">—</Typography>
                                                )}
                                            </TableCell>

                                            {/* Game */}
                                            <TableCell>
                                                {msg.game ? (
                                                    <Stack direction="row" spacing={0.5} alignItems="center">
                                                        <SportsEsportsRoundedIcon sx={{ fontSize: 15, color: 'text.secondary' }} />
                                                        {plainMatchLink(accountId, msg.game, navigate, `Game #${msg.game.slice(-5)}`)}
                                                    </Stack>
                                                ) : (
                                                    <Typography variant="caption" color="text.disabled">—</Typography>
                                                )}
                                            </TableCell>

                                            {/* Copy action */}
                                            <TableCell align="center">
                                                <Tooltip title={copiedIndex === index ? "Copied!" : "Copy message text"}>
                                                    <IconButton
                                                        size="small"
                                                        onClick={() => copyMessage(msg.message, index)}
                                                        sx={{
                                                            border: '1px solid',
                                                            borderColor: 'divider',
                                                            color: copiedIndex === index ? 'success.main' : 'inherit',
                                                        }}
                                                    >
                                                        <ContentCopyRoundedIcon fontSize="small" />
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
                        count={filteredMessages.length}
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
            {!loading && messages.length > 0 && (
                <Box sx={{ mt: 2, display: 'flex', justifyContent: 'center' }}>
                    <HistoryNavButtons
                        items={messages}
                        dateFunction={(m: ChatMessage) => m.time}
                        date={date}
                        setDate={setDate}
                        isBefore={isBefore}
                        setIsBefore={setIsBefore}
                        disabled={loading}
                        datePicker={false}
                        onChange={() => {
                            const newParams = new URLSearchParams(searchParams);
                            newParams.delete('limit');
                            setSearchParams(newParams);
                            setPage(0);
                        }}
                    />
                </Box>
            )}
        </Box>
    );
};