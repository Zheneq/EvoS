import React, {useCallback, useEffect, useMemo, useState} from 'react';
import {
    Alert,
    Box,
    Button,
    Card,
    Chip,
    Dialog,
    DialogActions,
    DialogContent,
    DialogContentText,
    DialogTitle,
    Grid,
    IconButton,
    InputAdornment,
    LinearProgress,
    Snackbar,
    Stack,
    Table,
    TableBody,
    TableCell,
    TableContainer,
    TableHead,
    TableRow,
    TextField,
    Tooltip,
    Typography,
} from '@mui/material';
import {
    formatDate,
    formatRelativeTime,
    GameServerKey,
    getGameServerKeys,
    setGameServerKeyStatus,
} from '../../lib/Evos';
import {useAuthHeader} from 'react-auth-kit';
import {useNavigate} from 'react-router-dom';
import {EvosError, processError} from '../../lib/Error';
import StorageRoundedIcon from '@mui/icons-material/StorageRounded';
import CheckCircleRoundedIcon from '@mui/icons-material/CheckCircleRounded';
import HourglassEmptyRoundedIcon from '@mui/icons-material/HourglassEmptyRounded';
import BlockRoundedIcon from '@mui/icons-material/BlockRounded';
import CloseRoundedIcon from '@mui/icons-material/CloseRounded';
import RefreshRoundedIcon from '@mui/icons-material/RefreshRounded';
import ContentCopyRoundedIcon from '@mui/icons-material/ContentCopyRounded';
import SearchRoundedIcon from '@mui/icons-material/SearchRounded';
import DnsRoundedIcon from '@mui/icons-material/DnsRounded';
import SecurityRoundedIcon from '@mui/icons-material/SecurityRounded';
import WarningAmberRoundedIcon from '@mui/icons-material/WarningAmberRounded';

const REFRESH_MS = 15000;

export default function GameServersPage() {
    const [keys, setKeys] = useState<GameServerKey[]>();
    const [error, setError] = useState<EvosError>();
    const [loading, setLoading] = useState<boolean>(true);
    const [searchQuery, setSearchQuery] = useState<string>('');
    const [statusFilter, setStatusFilter] = useState<string>('All');
    const [copiedFingerprint, setCopiedFingerprint] = useState<string>();
    const [confirmRevoke, setConfirmRevoke] = useState<GameServerKey>();
    const [confirmApprove, setConfirmApprove] = useState<GameServerKey>();
    const [actionMsg, setActionMsg] = useState<string>();

    const authHeader = useAuthHeader()();
    const navigate = useNavigate();

    const loadKeys = useCallback(() => {
        const abort = new AbortController();
        getGameServerKeys(abort, authHeader)
            .then(resp => {
                setKeys(resp.data.keys);
                setError(undefined);
            })
            .catch(e => processError(e, setError, navigate))
            .finally(() => setLoading(false));
        return () => abort.abort();
    }, [authHeader, navigate]);

    useEffect(() => {
        const cleanup = loadKeys();
        const interval = setInterval(loadKeys, REFRESH_MS);
        return () => {
            cleanup();
            clearInterval(interval);
        };
    }, [loadKeys]);

    const setStatus = (fingerprint: string, approve: boolean) => {
        setLoading(true);
        const abort = new AbortController();
        setGameServerKeyStatus(abort, authHeader, fingerprint, approve)
            .then(() => {
                setActionMsg(approve ? 'Server key approved successfully' : 'Server key revoked/declined');
                loadKeys();
            })
            .catch(e => {
                processError(e, setError, navigate);
                setLoading(false);
            });
    };

    const doRevoke = () => {
        if (!confirmRevoke) return;
        const fingerprint = confirmRevoke.fingerprint;
        setConfirmRevoke(undefined);
        setStatus(fingerprint, false);
    };

    const doApprove = () => {
        if (!confirmApprove) return;
        const fingerprint = confirmApprove.fingerprint;
        setConfirmApprove(undefined);
        setStatus(fingerprint, true);
    };

    const handleCopy = (text: string) => {
        navigator.clipboard.writeText(text).then(() => {
            setCopiedFingerprint(text);
        });
    };

    // Calculate metrics
    const totalCount = keys?.length ?? 0;
    const pendingCount = keys?.filter(k => k.status === 'Pending').length ?? 0;
    const approvedCount = keys?.filter(k => k.status === 'Approved').length ?? 0;
    const revokedCount = keys?.filter(k => k.status === 'Revoked' || k.status === 'Declined').length ?? 0;

    // Filter keys
    const filteredKeys = useMemo(() => {
        if (!keys) return [];
        return keys.filter(k => {
            // Status filter
            if (statusFilter === 'Pending' && k.status !== 'Pending') return false;
            if (statusFilter === 'Approved' && k.status !== 'Approved') return false;
            if (statusFilter === 'Revoked' && k.status !== 'Revoked' && k.status !== 'Declined') return false;

            // Search query
            if (searchQuery.trim()) {
                const q = searchQuery.toLowerCase().trim();
                const matchName = (k.lastName || '').toLowerCase().includes(q);
                const matchFp = k.fingerprint.toLowerCase().includes(q);
                const matchAddr = (k.lastConnectionAddress || '').toLowerCase().includes(q) ||
                    (k.lastActualAddress || '').toLowerCase().includes(q);
                const matchBuild = (k.lastBuildVersion || '').toLowerCase().includes(q);
                if (!matchName && !matchFp && !matchAddr && !matchBuild) {
                    return false;
                }
            }
            return true;
        });
    }, [keys, statusFilter, searchQuery]);

    const statusBadge = (status: string) => {
        switch (status) {
            case 'Approved':
                return (
                    <Chip
                        icon={<CheckCircleRoundedIcon />}
                        label="Approved"
                        color="success"
                        size="small"
                        sx={{ fontWeight: 600 }}
                    />
                );
            case 'Pending':
                return (
                    <Chip
                        icon={<HourglassEmptyRoundedIcon />}
                        label="Pending"
                        color="warning"
                        size="small"
                        sx={{ fontWeight: 600 }}
                    />
                );
            case 'Revoked':
            case 'Declined':
                return (
                    <Chip
                        icon={<BlockRoundedIcon />}
                        label={status}
                        color="error"
                        size="small"
                        sx={{ fontWeight: 600 }}
                    />
                );
            default:
                return <Chip label={status} size="small" />;
        }
    };

    return (
        <Box sx={{ width: '100%', display: 'flex', flexDirection: 'column', gap: 3 }}>
            {/* Header */}
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
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, flexWrap: 'wrap', mb: 0.5 }}>
                        <StorageRoundedIcon color="primary" sx={{ fontSize: 32 }} />
                        <Typography variant="h4" component="h1" sx={{ fontWeight: 700, letterSpacing: '-0.5px' }}>
                            Dedicated Game Servers
                        </Typography>
                        {pendingCount > 0 && (
                            <Chip
                                label={`${pendingCount} Pending Approval`}
                                color="warning"
                                icon={<HourglassEmptyRoundedIcon />}
                                size="small"
                                sx={{ fontWeight: 700 }}
                            />
                        )}
                    </Box>
                    <Typography variant="body2" color="text.secondary">
                        Authorize, inspect, and revoke cryptographic registration keys for dedicated game instances.
                    </Typography>
                </Box>

                <Button
                    variant="outlined"
                    size="small"
                    startIcon={<RefreshRoundedIcon />}
                    onClick={() => {
                        setLoading(true);
                        loadKeys();
                    }}
                    disabled={loading}
                >
                    Refresh List
                </Button>
            </Box>

            {/* Error Message */}
            {error && (
                <Alert severity="error" onClose={() => setError(undefined)}>
                    Failed to load server keys: {error.text} {error.description ? `(${error.description})` : ''}
                </Alert>
            )}

            {/* Pending Action Banner */}
            {pendingCount > 0 && (
                <Alert severity="warning" icon={<WarningAmberRoundedIcon fontSize="inherit" />}>
                    <strong>{pendingCount} game server key(s) awaiting approval.</strong> Dedicated instances require an approved key to register and receive game match allocations.
                </Alert>
            )}

            {/* Metrics Overview Cards */}
            <Grid container spacing={2}>
                <Grid size={{ xs: 6, sm: 3 }}>
                    <Card variant="outlined" sx={{ p: 2, display: 'flex', alignItems: 'center', gap: 2 }}>
                        <DnsRoundedIcon color="primary" sx={{ fontSize: 32, opacity: 0.8 }} />
                        <Box>
                            <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600 }}>
                                TOTAL KEYS
                            </Typography>
                            <Typography variant="h5" sx={{ fontWeight: 700 }}>
                                {totalCount}
                            </Typography>
                        </Box>
                    </Card>
                </Grid>
                <Grid size={{ xs: 6, sm: 3 }}>
                    <Card variant="outlined" sx={{ p: 2, display: 'flex', alignItems: 'center', gap: 2 }}>
                        <CheckCircleRoundedIcon color="success" sx={{ fontSize: 32, opacity: 0.8 }} />
                        <Box>
                            <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600 }}>
                                APPROVED
                            </Typography>
                            <Typography variant="h5" sx={{ fontWeight: 700, color: 'success.main' }}>
                                {approvedCount}
                            </Typography>
                        </Box>
                    </Card>
                </Grid>
                <Grid size={{ xs: 6, sm: 3 }}>
                    <Card variant="outlined" sx={{ p: 2, display: 'flex', alignItems: 'center', gap: 2 }}>
                        <HourglassEmptyRoundedIcon color="warning" sx={{ fontSize: 32, opacity: 0.8 }} />
                        <Box>
                            <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600 }}>
                                PENDING
                            </Typography>
                            <Typography variant="h5" sx={{ fontWeight: 700, color: pendingCount > 0 ? 'warning.main' : 'text.primary' }}>
                                {pendingCount}
                            </Typography>
                        </Box>
                    </Card>
                </Grid>
                <Grid size={{ xs: 6, sm: 3 }}>
                    <Card variant="outlined" sx={{ p: 2, display: 'flex', alignItems: 'center', gap: 2 }}>
                        <SecurityRoundedIcon color="error" sx={{ fontSize: 32, opacity: 0.8 }} />
                        <Box>
                            <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600 }}>
                                REVOKED
                            </Typography>
                            <Typography variant="h5" sx={{ fontWeight: 700, color: 'text.secondary' }}>
                                {revokedCount}
                            </Typography>
                        </Box>
                    </Card>
                </Grid>
            </Grid>

            {/* Keys Table Card */}
            <Card variant="outlined">
                {/* Search & Filter Toolbar */}
                <Box
                    sx={{
                        p: 2,
                        display: 'flex',
                        flexDirection: { xs: 'column', sm: 'row' },
                        justifyContent: 'space-between',
                        alignItems: { xs: 'stretch', sm: 'center' },
                        gap: 2,
                        borderBottom: '1px solid',
                        borderColor: 'divider',
                    }}
                >
                    <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
                        {['All', 'Pending', 'Approved', 'Revoked'].map(tab => (
                            <Chip
                                key={tab}
                                label={
                                    tab === 'All'
                                        ? `All (${totalCount})`
                                        : tab === 'Pending'
                                            ? `Pending (${pendingCount})`
                                            : tab === 'Approved'
                                                ? `Approved (${approvedCount})`
                                                : `Revoked (${revokedCount})`
                                }
                                onClick={() => setStatusFilter(tab)}
                                color={statusFilter === tab ? 'primary' : 'default'}
                                variant={statusFilter === tab ? 'filled' : 'outlined'}
                                size="small"
                                sx={{ fontWeight: 600, cursor: 'pointer' }}
                            />
                        ))}
                    </Stack>

                    <TextField
                        size="small"
                        placeholder="Search by name, fingerprint, IP, build..."
                        value={searchQuery}
                        onChange={(e) => setSearchQuery(e.target.value)}
                        slotProps={{
                            input: {
                                startAdornment: (
                                    <InputAdornment position="start">
                                        <SearchRoundedIcon fontSize="small" color="action" />
                                    </InputAdornment>
                                ),
                            }
                        }}
                        sx={{ minWidth: { xs: '100%', sm: 280 } }}
                    />
                </Box>

                {loading && <LinearProgress sx={{ borderRadius: 1 }} />}

                {/* Table */}
                <TableContainer>
                    <Table size="small">
                        <TableHead>
                            <TableRow>
                                <TableCell sx={{ fontWeight: 600 }}>Server Name</TableCell>
                                <TableCell sx={{ fontWeight: 600 }}>Fingerprint</TableCell>
                                <TableCell sx={{ fontWeight: 600 }}>Status</TableCell>
                                <TableCell sx={{ fontWeight: 600 }}>Actual Address</TableCell>
                                <TableCell sx={{ fontWeight: 600 }}>Connection Host</TableCell>
                                <TableCell sx={{ fontWeight: 600 }}>Build</TableCell>
                                <TableCell sx={{ fontWeight: 600 }}>First Seen</TableCell>
                                <TableCell sx={{ fontWeight: 600 }}>Last Connected</TableCell>
                                <TableCell align="right" sx={{ fontWeight: 600 }}>Actions</TableCell>
                            </TableRow>
                        </TableHead>
                        <TableBody>
                            {filteredKeys.length === 0 ? (
                                <TableRow>
                                    <TableCell colSpan={9} align="center" sx={{ py: 6 }}>
                                        <Typography variant="body2" color="text.secondary">
                                            {searchQuery ? 'No game server keys match your search criteria.' : 'No game server keys found.'}
                                        </Typography>
                                    </TableCell>
                                </TableRow>
                            ) : (
                                filteredKeys.map(k => {
                                    const isMoved =
                                        k.approvedActualAddress &&
                                        k.lastActualAddress &&
                                        k.approvedActualAddress !== k.lastActualAddress;

                                    return (
                                        <TableRow
                                            key={k.fingerprint}
                                            hover
                                            sx={{ '&:last-child td, &:last-child th': { border: 0 } }}
                                        >
                                            {/* Name */}
                                            <TableCell>
                                                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                                                    {k.lastName || <em style={{ color: 'gray' }}>unnamed server</em>}
                                                </Typography>
                                            </TableCell>

                                            {/* Fingerprint */}
                                            <TableCell>
                                                <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                                                    <Tooltip title={`Full Key: ${k.fingerprint}`}>
                                                        <code style={{ fontSize: '0.8rem', padding: '2px 4px', borderRadius: 4, background: 'rgba(128,128,128,0.1)' }}>
                                                            {k.fingerprint.substring(0, 12)}…
                                                        </code>
                                                    </Tooltip>
                                                    <Tooltip title={copiedFingerprint === k.fingerprint ? "Copied!" : "Copy fingerprint"}>
                                                        <IconButton
                                                            size="small"
                                                            onClick={() => handleCopy(k.fingerprint)}
                                                            sx={{ p: 0.5 }}
                                                        >
                                                            <ContentCopyRoundedIcon sx={{ fontSize: '0.9rem' }} />
                                                        </IconButton>
                                                    </Tooltip>
                                                </Box>
                                            </TableCell>

                                            {/* Status */}
                                            <TableCell>
                                                {statusBadge(k.status)}
                                            </TableCell>

                                            {/* Actual Address */}
                                            <TableCell>
                                                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                                                    <Typography variant="body2">
                                                        {k.lastActualAddress || '—'}
                                                    </Typography>
                                                    {isMoved && (
                                                        <Tooltip title={`Pinned to ${k.approvedActualAddress}; connected from ${k.lastActualAddress}. Address changed — verify server before approving.`}>
                                                            <Chip
                                                                label="Moved"
                                                                color="warning"
                                                                size="small"
                                                                icon={<WarningAmberRoundedIcon />}
                                                                sx={{ height: 20, fontSize: '0.7rem' }}
                                                            />
                                                        </Tooltip>
                                                    )}
                                                </Box>
                                            </TableCell>

                                            {/* Last Connection Address */}
                                            <TableCell>
                                                <Typography variant="body2" color="text.secondary">
                                                    {k.lastConnectionAddress || '—'}
                                                </Typography>
                                            </TableCell>

                                            {/* Build */}
                                            <TableCell>
                                                {k.lastBuildVersion ? (
                                                    <Typography variant="caption" sx={{ fontFamily: 'monospace' }}>
                                                        {k.lastBuildVersion.replace(/^STABLE-122-100_/, '')}
                                                    </Typography>
                                                ) : (
                                                    '—'
                                                )}
                                            </TableCell>

                                            {/* First Seen */}
                                            <TableCell>
                                                <Typography variant="body2">
                                                    {formatDate(k.firstSeenAt)}
                                                </Typography>
                                                <Typography variant="caption" color="text.secondary" display="block">
                                                    {formatRelativeTime(k.firstSeenAt)}
                                                </Typography>
                                            </TableCell>

                                            {/* Last Connected */}
                                            <TableCell>
                                                {k.lastConnectedAt ? (
                                                    <>
                                                        <Typography variant="body2">
                                                            {formatDate(k.lastConnectedAt)}
                                                        </Typography>
                                                        <Typography variant="caption" color="text.secondary" display="block">
                                                            {formatRelativeTime(k.lastConnectedAt)}
                                                        </Typography>
                                                    </>
                                                ) : (
                                                    <Typography variant="caption" color="text.secondary">
                                                        Never
                                                    </Typography>
                                                )}
                                            </TableCell>

                                            {/* Actions */}
                                            <TableCell align="right">
                                                <Stack direction="row" spacing={1} sx={{ justifyContent: 'flex-end' }}>
                                                    {k.status === 'Pending' && (
                                                        <>
                                                            <Button
                                                                size="small"
                                                                color="success"
                                                                variant="contained"
                                                                onClick={() => setStatus(k.fingerprint, true)}
                                                                startIcon={<CheckCircleRoundedIcon />}
                                                            >
                                                                Approve
                                                            </Button>
                                                            <Button
                                                                size="small"
                                                                color="error"
                                                                variant="outlined"
                                                                onClick={() => setStatus(k.fingerprint, false)}
                                                                startIcon={<CloseRoundedIcon />}
                                                            >
                                                                Decline
                                                            </Button>
                                                        </>
                                                    )}
                                                    {k.status === 'Approved' && (
                                                        <Button
                                                            size="small"
                                                            color="error"
                                                            variant="outlined"
                                                            onClick={() => setConfirmRevoke(k)}
                                                            startIcon={<BlockRoundedIcon />}
                                                        >
                                                            Revoke
                                                        </Button>
                                                    )}
                                                    {(k.status === 'Declined' || k.status === 'Revoked') && (
                                                        <Button
                                                            size="small"
                                                            color="success"
                                                            variant="outlined"
                                                            onClick={() => setConfirmApprove(k)}
                                                            startIcon={<CheckCircleRoundedIcon />}
                                                        >
                                                            Approve
                                                        </Button>
                                                    )}
                                                </Stack>
                                            </TableCell>
                                        </TableRow>
                                    );
                                })
                            )}
                        </TableBody>
                    </Table>
                </TableContainer>
            </Card>

            {/* Revoke Confirmation Dialog */}
            <Dialog
                open={!!confirmRevoke}
                onClose={() => setConfirmRevoke(undefined)}
                maxWidth="xs"
                fullWidth
            >
                <DialogTitle sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                    <BlockRoundedIcon color="error" />
                    Revoke Game Server Key?
                </DialogTitle>
                <DialogContent>
                    <DialogContentText>
                        Are you sure you want to revoke key for <strong>{confirmRevoke?.lastName || 'unnamed server'}</strong> (<code>{confirmRevoke?.fingerprint.substring(0, 12)}…</code>)?
                    </DialogContentText>
                    <DialogContentText sx={{ mt: 1.5, fontSize: '0.85rem' }}>
                        This immediately disconnects any game server currently using this key and prevents it from reconnecting or receiving match allocations until re-approved.
                    </DialogContentText>
                </DialogContent>
                <DialogActions sx={{ px: 3, pb: 2 }}>
                    <Button onClick={() => setConfirmRevoke(undefined)}>
                        Cancel
                    </Button>
                    <Button variant="contained" color="error" onClick={doRevoke}>
                        Revoke Key
                    </Button>
                </DialogActions>
            </Dialog>

            {/* Re-approve Confirmation Dialog */}
            <Dialog
                open={!!confirmApprove}
                onClose={() => setConfirmApprove(undefined)}
                maxWidth="xs"
                fullWidth
            >
                <DialogTitle sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                    <CheckCircleRoundedIcon color="success" />
                    Approve Game Server Key?
                </DialogTitle>
                <DialogContent>
                    <DialogContentText>
                        Re-approve key for <strong>{confirmApprove?.lastName || 'unnamed server'}</strong> (<code>{confirmApprove?.fingerprint.substring(0, 12)}…</code>)?
                    </DialogContentText>
                    <DialogContentText sx={{ mt: 1.5, fontSize: '0.85rem' }}>
                        The server will be authorized and go live the next time it reconnects to the master lobby.
                    </DialogContentText>
                </DialogContent>
                <DialogActions sx={{ px: 3, pb: 2 }}>
                    <Button onClick={() => setConfirmApprove(undefined)}>
                        Cancel
                    </Button>
                    <Button variant="contained" color="success" onClick={doApprove}>
                        Approve Key
                    </Button>
                </DialogActions>
            </Dialog>

            {/* Action Feedback Toast */}
            <Snackbar
                open={!!actionMsg}
                autoHideDuration={4000}
                onClose={() => setActionMsg(undefined)}
                anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
            >
                <Alert onClose={() => setActionMsg(undefined)} severity="success" variant="filled" sx={{ width: '100%' }}>
                    {actionMsg}
                </Alert>
            </Snackbar>
        </Box>
    );
}
