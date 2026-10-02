import React, {useCallback, useEffect, useMemo, useState} from "react";
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
} from "@mui/material";
import {
    confirmUsernameRequest,
    declineUsernameRequest,
    formatDate,
    formatRelativeTime,
    getRegistrationCodes,
    getUsernameRequests,
    issueRegistrationCode,
    RegistrationCodeEntry,
    UsernameRequestEntry,
} from "../../lib/Evos";
import {useAuthHeader} from "react-auth-kit";
import {useNavigate} from "react-router-dom";
import {EvosError, processError} from "../../lib/Error";
import {plainAccountLink} from "../generic/BasicComponents";
import DiscordUser from "../generic/DiscordUser";
import ConfirmationNumberRoundedIcon from "@mui/icons-material/ConfirmationNumberRounded";
import VpnKeyRoundedIcon from "@mui/icons-material/VpnKeyRounded";
import GroupAddRoundedIcon from "@mui/icons-material/GroupAddRounded";
import CheckCircleRoundedIcon from "@mui/icons-material/CheckCircleRounded";
import CloseRoundedIcon from "@mui/icons-material/CloseRounded";
import SearchRoundedIcon from "@mui/icons-material/SearchRounded";
import RefreshRoundedIcon from "@mui/icons-material/RefreshRounded";
import ContentCopyRoundedIcon from "@mui/icons-material/ContentCopyRounded";
import LockOpenRoundedIcon from "@mui/icons-material/LockOpenRounded";
import TimerOffRoundedIcon from "@mui/icons-material/TimerOffRounded";
import HourglassEmptyRoundedIcon from "@mui/icons-material/HourglassEmptyRounded";
import WarningAmberRoundedIcon from "@mui/icons-material/WarningAmberRounded";

export default function IssueRegistrationCode() {
    const [issueFor, setIssueFor] = useState<string>("");
    const [generatedCode, setGeneratedCode] = useState<string>();
    const [processing, setProcessing] = useState<boolean>(false);
    const [codes, setCodes] = useState<RegistrationCodeEntry[]>();
    const [codesBefore, setCodesBefore] = useState<Date>(new Date());
    const [error, setError] = useState<EvosError>();
    const [requests, setRequests] = useState<UsernameRequestEntry[]>();
    const [requestsError, setRequestsError] = useState<EvosError>();
    const [requestsRefresh, setRequestsRefresh] = useState<number>(0);
    const [declineTarget, setDeclineTarget] = useState<UsernameRequestEntry>();
    const [declineReason, setDeclineReason] = useState<string>("");
    const [searchQuery, setSearchQuery] = useState<string>("");
    const [statusFilter, setStatusFilter] = useState<string>("All");
    const [copiedCode, setCopiedCode] = useState<string>();
    const [snackbarMsg, setSnackbarMsg] = useState<string>();

    const authHeader = useAuthHeader()();
    const navigate = useNavigate();

    const refresh = useCallback(() => {
        setRequestsRefresh(x => x + 1);
        setCodesBefore(new Date(new Date().getTime() + 60000));
    }, []);

    const handleCopy = (text: string) => {
        navigator.clipboard.writeText(text).then(() => {
            setCopiedCode(text);
            setSnackbarMsg("Registration code copied to clipboard!");
        });
    };

    const handleIssueSubmit = (event: React.FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        const trimmed = issueFor.trim();
        if (!trimmed) return;

        setProcessing(true);
        const abort = new AbortController();
        issueRegistrationCode(abort, authHeader, {issueFor: trimmed})
            .then((resp) => {
                setGeneratedCode(resp.data.code);
                setIssueFor("");
                handleCopy(resp.data.code);
                refresh();
            })
            .catch(e => processError(e, err => setError(err), navigate))
            .finally(() => setProcessing(false));

        return () => abort.abort();
    };

    useEffect(() => {
        const abort = new AbortController();
        getRegistrationCodes(abort, authHeader, codesBefore)
            .then((resp) => {
                setError(undefined);
                setCodes(resp.data.entries);
            })
            .catch((err) => {
                if (abort.signal.aborted) return;
                processError(err, setError, navigate);
            });

        return () => abort.abort();
    }, [authHeader, navigate, codesBefore]);

    useEffect(() => {
        const abort = new AbortController();
        getUsernameRequests(abort, authHeader)
            .then((resp) => {
                setRequestsError(undefined);
                setRequests(resp.data.entries);
            })
            .catch((err) => {
                if (abort.signal.aborted) return;
                processError(err, setRequestsError, navigate);
            });

        return () => abort.abort();
    }, [authHeader, navigate, requestsRefresh]);

    const handleConfirmRequest = (row: UsernameRequestEntry) => {
        setProcessing(true);
        const abort = new AbortController();
        confirmUsernameRequest(abort, authHeader, row.discordUserId, row.requestedUsername)
            .then(() => {
                setSnackbarMsg(`Approved username request for "${row.requestedUsername}"`);
                refresh();
            })
            .catch(e => processError(e, setRequestsError, navigate))
            .finally(() => setProcessing(false));
    };

    const submitDeclineRequest = () => {
        if (!declineTarget) return;
        setProcessing(true);
        const abort = new AbortController();
        declineUsernameRequest(abort, authHeader, declineTarget.discordUserId, declineTarget.requestedUsername, declineReason)
            .then(() => {
                setSnackbarMsg(`Declined request for "${declineTarget.requestedUsername}"`);
                setDeclineTarget(undefined);
                setDeclineReason("");
                refresh();
            })
            .catch(e => processError(e, setRequestsError, navigate))
            .finally(() => setProcessing(false));
    };

    // Calculate metrics
    const totalCodes = codes?.length ?? 0;
    const now = useMemo(() => new Date(), []);
    const claimedCodes = codes?.filter(c => c.issuedTo !== 0).length ?? 0;
    const activeCodes = codes?.filter(c => c.issuedTo === 0 && new Date(c.expiresAt) >= now).length ?? 0;
    const expiredCodes = codes?.filter(c => c.issuedTo === 0 && new Date(c.expiresAt) < now).length ?? 0;
    const pendingRequestsCount = requests?.length ?? 0;

    // Filter codes
    const filteredCodes = useMemo(() => {
        if (!codes) return [];
        return codes.filter(c => {
            const isClaimed = c.issuedTo !== 0;
            const isExpired = !isClaimed && new Date(c.expiresAt) < now;
            const isActive = !isClaimed && !isExpired;

            if (statusFilter === "Active" && !isActive) return false;
            if (statusFilter === "Claimed" && !isClaimed) return false;
            if (statusFilter === "Expired" && !isExpired) return false;

            if (searchQuery.trim()) {
                const q = searchQuery.toLowerCase().trim();
                const matchCode = c.code.toLowerCase().includes(q);
                const matchTarget = (c.issuedToHandle || "").toLowerCase().includes(q);
                const matchIssuer = (c.issuedByHandle || "").toLowerCase().includes(q);
                const matchDiscord = (c.discordDisplayName || "").toLowerCase().includes(q) ||
                    (c.discordUserName || "").toLowerCase().includes(q) ||
                    (c.discordUserId || "").toLowerCase().includes(q);

                if (!matchCode && !matchTarget && !matchIssuer && !matchDiscord) {
                    return false;
                }
            }
            return true;
        });
    }, [codes, statusFilter, searchQuery, now]);

    return (
        <Box sx={{ width: '100%', display: 'flex', flexDirection: 'column', gap: 3 }}>
            {/* Page Header */}
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
                        <ConfirmationNumberRoundedIcon color="primary" sx={{ fontSize: 32 }} />
                        <Typography variant="h4" component="h1" sx={{ fontWeight: 700, letterSpacing: '-0.5px' }}>
                            Registration & Invite Codes
                        </Typography>
                        {pendingRequestsCount > 0 && (
                            <Chip
                                label={`${pendingRequestsCount} Username Requests`}
                                color="warning"
                                icon={<HourglassEmptyRoundedIcon />}
                                size="small"
                                sx={{ fontWeight: 700 }}
                            />
                        )}
                    </Box>
                    <Typography variant="body2" color="text.secondary">
                        Issue signup codes, manage player invites, and review pending Discord username requests.
                    </Typography>
                </Box>

                <Button
                    variant="outlined"
                    size="small"
                    startIcon={<RefreshRoundedIcon />}
                    onClick={refresh}
                    disabled={processing}
                >
                    Refresh Data
                </Button>
            </Box>

            {/* Error Notifications */}
            {error && (
                <Alert severity="error" onClose={() => setError(undefined)}>
                    Failed to load registration codes: {error.text} {error.description ? `(${error.description})` : ""}
                </Alert>
            )}

            {requestsError && (
                <Alert severity="error" onClose={() => setRequestsError(undefined)}>
                    Failed to load username requests: {requestsError.text} {requestsError.description ? `(${requestsError.description})` : ""}
                </Alert>
            )}

            {/* Section 1: Issue Code Card & Metric Cards */}
            <Grid container spacing={3}>
                {/* Issue Code Form Card */}
                <Grid size={{ xs: 12, md: 5 }}>
                    <Card variant="outlined" sx={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
                        <Box sx={{ p: 2.5, pb: 1.5 }}>
                            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25, mb: 0.5 }}>
                                <VpnKeyRoundedIcon color="primary" />
                                <Typography variant="h6" sx={{ fontWeight: 600 }}>
                                    Issue Invite Code
                                </Typography>
                            </Box>
                            <Typography variant="body2" color="text.secondary">
                                Generate a single-use registration key for a new player.
                            </Typography>
                        </Box>

                        <Box
                            component="form"
                            onSubmit={handleIssueSubmit}
                            noValidate
                            sx={{ p: 2.5, pt: 0, flexGrow: 1, display: 'flex', flexDirection: 'column', gap: 2 }}
                        >
                            <TextField
                                fullWidth
                                required
                                size="small"
                                id="issueFor"
                                label="Issue For (Recipient Handle / Discord Tag)"
                                placeholder="e.g. PlayerAlias or Discord#0000"
                                value={issueFor}
                                onChange={(e) => setIssueFor(e.target.value)}
                                disabled={processing}
                                helperText="Enter the intended recipient's username or alias for recordkeeping."
                            />

                            <Button
                                type="submit"
                                variant="contained"
                                color="primary"
                                fullWidth
                                disabled={processing || !issueFor.trim()}
                                startIcon={<VpnKeyRoundedIcon />}
                            >
                                Generate Registration Code
                            </Button>

                            {/* Newly Generated Code Banner */}
                            {generatedCode && (
                                <Alert
                                    severity="success"
                                    icon={<CheckCircleRoundedIcon />}
                                    sx={{
                                        mt: 1,
                                        display: 'flex',
                                        alignItems: 'center',
                                        '& .MuiAlert-message': { width: '100%' }
                                    }}
                                >
                                    <Typography variant="caption" sx={{ fontWeight: 700, display: 'block', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                                        New Code Generated
                                    </Typography>
                                    <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 1, mt: 0.5 }}>
                                        <code style={{ fontSize: '1.1rem', fontWeight: 700, letterSpacing: '0.06em' }}>
                                            {generatedCode}
                                        </code>
                                        <Button
                                            size="small"
                                            variant="outlined"
                                            color="success"
                                            startIcon={<ContentCopyRoundedIcon />}
                                            onClick={() => handleCopy(generatedCode)}
                                            sx={{ textTransform: 'none', py: 0.25 }}
                                        >
                                            Copy
                                        </Button>
                                    </Box>
                                </Alert>
                            )}
                        </Box>

                        {processing && <LinearProgress sx={{ borderRadius: 1 }} />}
                    </Card>
                </Grid>

                {/* Metrics Summary Grid */}
                <Grid size={{ xs: 12, md: 7 }}>
                    <Grid container spacing={2} sx={{ height: '100%' }}>
                        <Grid size={{ xs: 6, sm: 6 }}>
                            <Card variant="outlined" sx={{ p: 2.5, height: '100%', display: 'flex', flexDirection: 'column', justifyContent: 'center' }}>
                                <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700, letterSpacing: '0.05em' }}>
                                    TOTAL ISSUED CODES
                                </Typography>
                                <Typography variant="h4" sx={{ fontWeight: 700, mt: 0.5 }}>
                                    {totalCodes}
                                </Typography>
                                <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5 }}>
                                    Cumulative registration codes created
                                </Typography>
                            </Card>
                        </Grid>
                        <Grid size={{ xs: 6, sm: 6 }}>
                            <Card variant="outlined" sx={{ p: 2.5, height: '100%', display: 'flex', flexDirection: 'column', justifyContent: 'center' }}>
                                <Typography variant="caption" color="primary" sx={{ fontWeight: 700, letterSpacing: '0.05em' }}>
                                    ACTIVE / UNCLAIMED
                                </Typography>
                                <Typography variant="h4" sx={{ fontWeight: 700, mt: 0.5, color: 'primary.main' }}>
                                    {activeCodes}
                                </Typography>
                                <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5 }}>
                                    Ready to be redeemed by new users
                                </Typography>
                            </Card>
                        </Grid>
                        <Grid size={{ xs: 6, sm: 6 }}>
                            <Card variant="outlined" sx={{ p: 2.5, height: '100%', display: 'flex', flexDirection: 'column', justifyContent: 'center' }}>
                                <Typography variant="caption" color="success.main" sx={{ fontWeight: 700, letterSpacing: '0.05em' }}>
                                    CLAIMED CODES
                                </Typography>
                                <Typography variant="h4" sx={{ fontWeight: 700, mt: 0.5, color: 'success.main' }}>
                                    {claimedCodes}
                                </Typography>
                                <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5 }}>
                                    Successfully redeemed for an account
                                </Typography>
                            </Card>
                        </Grid>
                        <Grid size={{ xs: 6, sm: 6 }}>
                            <Card variant="outlined" sx={{ p: 2.5, height: '100%', display: 'flex', flexDirection: 'column', justifyContent: 'center' }}>
                                <Typography variant="caption" color={pendingRequestsCount > 0 ? "warning.main" : "text.secondary"} sx={{ fontWeight: 700, letterSpacing: '0.05em' }}>
                                    PENDING REQUESTS
                                </Typography>
                                <Typography variant="h4" sx={{ fontWeight: 700, mt: 0.5, color: pendingRequestsCount > 0 ? "warning.main" : "text.primary" }}>
                                    {pendingRequestsCount}
                                </Typography>
                                <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5 }}>
                                    Discord username requests waiting
                                </Typography>
                            </Card>
                        </Grid>
                    </Grid>
                </Grid>
            </Grid>

            {/* Section 2: Pending Username Requests (Conditional Card) */}
            {requests && requests.length > 0 && (
                <Card variant="outlined">
                    <Box sx={{ p: 2.5, pb: 1.5, display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 1 }}>
                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25 }}>
                            <GroupAddRoundedIcon color="warning" />
                            <Box>
                                <Typography variant="h6" sx={{ fontWeight: 600 }}>
                                    Pending Username Requests ({requests.length})
                                </Typography>
                                <Typography variant="body2" color="text.secondary">
                                    Approve or decline custom username registrations requested through Discord.
                                </Typography>
                            </Box>
                        </Box>
                    </Box>

                    <TableContainer>
                        <Table size="small">
                            <TableHead>
                                <TableRow>
                                    <TableCell sx={{ fontWeight: 600 }}>Requested Username</TableCell>
                                    <TableCell sx={{ fontWeight: 600 }}>Discord Identity</TableCell>
                                    <TableCell sx={{ fontWeight: 600 }}>Account Created</TableCell>
                                    <TableCell sx={{ fontWeight: 600 }}>Server Joined</TableCell>
                                    <TableCell sx={{ fontWeight: 600 }}>Requested At</TableCell>
                                    <TableCell align="right" sx={{ fontWeight: 600 }}>Actions</TableCell>
                                </TableRow>
                            </TableHead>
                            <TableBody>
                                {requests.map((row) => (
                                    <TableRow key={`${row.discordUserId}:${row.requestedUsername}`} hover>
                                        <TableCell>
                                            <Typography variant="body2" sx={{ fontWeight: 700 }}>
                                                {row.requestedUsername}
                                            </Typography>
                                        </TableCell>
                                        <TableCell>
                                            <DiscordUser
                                                displayName={row.discordDisplayName}
                                                userName={row.discordUserName}
                                                userId={row.discordUserId}
                                                avatarUrl={row.discordAvatarUrl}
                                            />
                                        </TableCell>
                                        <TableCell>
                                            <Typography variant="body2">{formatDate(row.discordCreatedAt)}</Typography>
                                            <Typography variant="caption" color="text.secondary" display="block">
                                                {formatRelativeTime(row.discordCreatedAt)}
                                            </Typography>
                                        </TableCell>
                                        <TableCell>
                                            {row.discordJoinedAt ? (
                                                <>
                                                    <Typography variant="body2">{formatDate(row.discordJoinedAt)}</Typography>
                                                    <Typography variant="caption" color="text.secondary" display="block">
                                                        {formatRelativeTime(row.discordJoinedAt)}
                                                    </Typography>
                                                </>
                                            ) : (
                                                "—"
                                            )}
                                        </TableCell>
                                        <TableCell>
                                            <Typography variant="body2">{formatDate(row.requestedAt)}</Typography>
                                            <Typography variant="caption" color="text.secondary" display="block">
                                                {formatRelativeTime(row.requestedAt)}
                                            </Typography>
                                        </TableCell>
                                        <TableCell align="right">
                                            <Stack direction="row" spacing={1} sx={{ justifyContent: 'flex-end' }}>
                                                <Button
                                                    disabled={processing}
                                                    variant="contained"
                                                    color="success"
                                                    size="small"
                                                    startIcon={<CheckCircleRoundedIcon />}
                                                    onClick={() => handleConfirmRequest(row)}
                                                >
                                                    Approve
                                                </Button>
                                                <Button
                                                    disabled={processing}
                                                    variant="outlined"
                                                    color="error"
                                                    size="small"
                                                    startIcon={<CloseRoundedIcon />}
                                                    onClick={() => setDeclineTarget(row)}
                                                >
                                                    Decline
                                                </Button>
                                            </Stack>
                                        </TableCell>
                                    </TableRow>
                                ))}
                            </TableBody>
                        </Table>
                    </TableContainer>
                </Card>
            )}

            {/* Section 3: Issued Registration Codes Table Card */}
            <Card variant="outlined">
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
                        {["All", "Active", "Claimed", "Expired"].map(tab => (
                            <Chip
                                key={tab}
                                label={
                                    tab === "All"
                                        ? `All (${totalCodes})`
                                        : tab === "Active"
                                            ? `Active (${activeCodes})`
                                            : tab === "Claimed"
                                                ? `Claimed (${claimedCodes})`
                                                : `Expired (${expiredCodes})`
                                }
                                onClick={() => setStatusFilter(tab)}
                                color={statusFilter === tab ? "primary" : "default"}
                                variant={statusFilter === tab ? "filled" : "outlined"}
                                size="small"
                                sx={{ fontWeight: 600, cursor: 'pointer' }}
                            />
                        ))}
                    </Stack>

                    <TextField
                        size="small"
                        placeholder="Search code, player, or Discord user..."
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

                <TableContainer>
                    <Table size="small">
                        <TableHead>
                            <TableRow>
                                <TableCell sx={{ fontWeight: 600 }}>Registration Code</TableCell>
                                <TableCell sx={{ fontWeight: 600 }}>Status</TableCell>
                                <TableCell sx={{ fontWeight: 600 }}>Issued To / Claimed By</TableCell>
                                <TableCell sx={{ fontWeight: 600 }}>Discord Identity</TableCell>
                                <TableCell sx={{ fontWeight: 600 }}>Issued By</TableCell>
                                <TableCell sx={{ fontWeight: 600 }}>Issued At</TableCell>
                                <TableCell sx={{ fontWeight: 600 }}>Expires At</TableCell>
                            </TableRow>
                        </TableHead>
                        <TableBody>
                            {filteredCodes.length === 0 ? (
                                <TableRow>
                                    <TableCell colSpan={7} align="center" sx={{ py: 6 }}>
                                        <Typography variant="body2" color="text.secondary">
                                            {searchQuery ? "No registration codes match your search query." : "No registration codes found."}
                                        </Typography>
                                    </TableCell>
                                </TableRow>
                            ) : (
                                filteredCodes.map((row) => {
                                    const claimed = row.issuedTo !== 0;
                                    const expired = !claimed && new Date(row.expiresAt) < now;

                                    return (
                                        <TableRow
                                            key={row.code}
                                            hover
                                            sx={{ '&:last-child td, &:last-child th': { border: 0 } }}
                                        >
                                            {/* Code & Copy */}
                                            <TableCell>
                                                <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75 }}>
                                                    <code style={{
                                                        fontSize: '0.85rem',
                                                        fontWeight: 600,
                                                        letterSpacing: '0.04em',
                                                        textDecoration: claimed || expired ? 'line-through' : 'none',
                                                        opacity: claimed || expired ? 0.7 : 1,
                                                        background: 'rgba(128,128,128,0.1)',
                                                        padding: '2px 6px',
                                                        borderRadius: 4
                                                    }}>
                                                        {row.code}
                                                    </code>
                                                    <Tooltip title={copiedCode === row.code ? "Copied!" : "Copy code"}>
                                                        <IconButton
                                                            size="small"
                                                            onClick={() => handleCopy(row.code)}
                                                            sx={{ p: 0.5 }}
                                                        >
                                                            <ContentCopyRoundedIcon sx={{ fontSize: '0.85rem' }} />
                                                        </IconButton>
                                                    </Tooltip>
                                                </Box>
                                            </TableCell>

                                            {/* Status Badge */}
                                            <TableCell>
                                                {claimed ? (
                                                    <Chip
                                                        label="Claimed"
                                                        color="success"
                                                        size="small"
                                                        icon={<CheckCircleRoundedIcon />}
                                                        sx={{ fontWeight: 600 }}
                                                    />
                                                ) : expired ? (
                                                    <Chip
                                                        label="Expired"
                                                        size="small"
                                                        icon={<TimerOffRoundedIcon />}
                                                        variant="outlined"
                                                    />
                                                ) : (
                                                    <Chip
                                                        label="Active"
                                                        color="primary"
                                                        size="small"
                                                        icon={<LockOpenRoundedIcon />}
                                                        sx={{ fontWeight: 600 }}
                                                    />
                                                )}
                                            </TableCell>

                                            {/* Issued To / Claimed By */}
                                            <TableCell>
                                                {claimed ? (
                                                    plainAccountLink(row.issuedTo, row.issuedToHandle, navigate)
                                                ) : (
                                                    <Typography variant="body2" sx={{ fontWeight: 500 }}>
                                                        {row.issuedToHandle}
                                                    </Typography>
                                                )}
                                            </TableCell>

                                            {/* Discord User */}
                                            <TableCell>
                                                <DiscordUser
                                                    displayName={row.discordDisplayName}
                                                    userName={row.discordUserName}
                                                    userId={row.discordUserId}
                                                    avatarUrl={row.discordAvatarUrl}
                                                />
                                            </TableCell>

                                            {/* Issued By */}
                                            <TableCell>
                                                {plainAccountLink(row.issuedBy, row.issuedByHandle, navigate)}
                                            </TableCell>

                                            {/* Issued At */}
                                            <TableCell>
                                                <Typography variant="body2">{formatDate(row.issuedAt)}</Typography>
                                                <Typography variant="caption" color="text.secondary" display="block">
                                                    {formatRelativeTime(row.issuedAt)}
                                                </Typography>
                                            </TableCell>

                                            {/* Expires At */}
                                            <TableCell>
                                                <Typography variant="body2">{formatDate(row.expiresAt)}</Typography>
                                                <Typography variant="caption" color="text.secondary" display="block">
                                                    {formatRelativeTime(row.expiresAt)}
                                                </Typography>
                                            </TableCell>
                                        </TableRow>
                                    );
                                })
                            )}
                        </TableBody>
                    </Table>
                </TableContainer>
            </Card>

            {/* Decline Request Dialog */}
            <Dialog
                open={declineTarget !== undefined}
                onClose={() => setDeclineTarget(undefined)}
                maxWidth="sm"
                fullWidth
            >
                <DialogTitle sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                    <WarningAmberRoundedIcon color="warning" />
                    Decline Username Request
                </DialogTitle>
                <DialogContent>
                    <DialogContentText>
                        Decline username request for <strong>{declineTarget?.requestedUsername}</strong>?
                        This rejection notice and reason will be automatically sent to the applicant via Discord.
                    </DialogContentText>
                    <TextField
                        autoFocus
                        margin="normal"
                        fullWidth
                        multiline
                        rows={3}
                        label="Reason for Decline"
                        placeholder="e.g. Inappropriate handle, reserved alias, or duplicate account."
                        value={declineReason}
                        onChange={(e) => setDeclineReason(e.target.value)}
                    />
                </DialogContent>
                <DialogActions sx={{ px: 3, pb: 2 }}>
                    <Button onClick={() => setDeclineTarget(undefined)}>
                        Cancel
                    </Button>
                    <Button
                        color="error"
                        variant="contained"
                        disabled={processing}
                        onClick={submitDeclineRequest}
                    >
                        Decline Request
                    </Button>
                </DialogActions>
            </Dialog>

            {/* Notification Toast */}
            <Snackbar
                open={!!snackbarMsg}
                autoHideDuration={4000}
                onClose={() => setSnackbarMsg(undefined)}
                anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
            >
                <Alert onClose={() => setSnackbarMsg(undefined)} severity="info" variant="filled" sx={{ width: '100%' }}>
                    {snackbarMsg}
                </Alert>
            </Snackbar>
        </Box>
    );
}
