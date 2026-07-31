import {
    Box,
    Button,
    Dialog,
    DialogActions,
    DialogContent,
    DialogContentText,
    DialogTitle,
    LinearProgress,
    Stack,
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableRow,
    TextField,
    Tooltip,
    Typography
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
    UsernameRequestEntry
} from "../../lib/Evos";
import React, {useCallback, useEffect, useState} from "react";
import {useAuthHeader} from "react-auth-kit";
import {useNavigate} from "react-router-dom";
import {EvosError, processError} from "../../lib/Error";
import BaseDialog from "../generic/BaseDialog";
import {EvosCard, FlexBox, plainAccountLink} from "../generic/BasicComponents";
import DiscordUser from "../generic/DiscordUser";

export default function IssueRegistrationCode() {
    const [code, setCode] = useState<string>();
    const [processing, setProcessing] = useState<boolean>();
    const [codes, setCodes] = useState<RegistrationCodeEntry[]>();
    const [codesBefore, setCodesBefore] = useState<Date>(new Date());
    const [error, setError] = useState<EvosError>();
    const [requests, setRequests] = useState<UsernameRequestEntry[]>();
    const [requestsError, setRequestsError] = useState<EvosError>();
    const [requestsRefresh, setRequestsRefresh] = useState<number>(0);
    const [declineTarget, setDeclineTarget] = useState<UsernameRequestEntry>();
    const [declineReason, setDeclineReason] = useState<string>("");
    const authHeader = useAuthHeader()();
    const navigate = useNavigate();

    const refresh = useCallback(() => {
        setRequestsRefresh(x => x + 1);
        setCodesBefore(new Date(new Date().getTime() + 60000));
    }, []);

    const handleSubmit = (event: React.FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        const data = new FormData(event.currentTarget);
        const issueFor = data.get('issueFor') as string;

        if (!issueFor) {
            return;
        }

        setProcessing(true);
        const abort = new AbortController();
        issueRegistrationCode(abort, authHeader, {issueFor: issueFor})
            .then((resp) => setCode(resp.data.code))
            .catch(e => processError(e, err => setCode(err.text), navigate))
            .then(() => {
                setProcessing(false);
                setCodesBefore(new Date(new Date().getTime() + 60000));
            });

        return () => abort.abort();
    };

    useEffect(() => {
        const abort = new AbortController();
        getRegistrationCodes(abort, authHeader, codesBefore)
            .then((resp) => {
                setError(undefined);
                setCodes(resp.data.entries);
            })
            .catch((error) => {
                if (abort.signal.aborted) {
                    return;
                }
                processError(error, setError, navigate);
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
            .catch((error) => {
                if (abort.signal.aborted) {
                    return;
                }
                processError(error, setRequestsError, navigate);
            });

        return () => abort.abort();
    }, [authHeader, navigate, requestsRefresh]);

    const handleConfirm = (row: UsernameRequestEntry) => {
        setProcessing(true);
        const abort = new AbortController();
        confirmUsernameRequest(abort, authHeader, row.discordUserId, row.requestedUsername)
            .catch(e => processError(e, setRequestsError, navigate))
            .then(() => {
                setProcessing(false);
                refresh();
            });
    };

    const submitDecline = () => {
        if (!declineTarget) {
            return;
        }
        setProcessing(true);
        const abort = new AbortController();
        declineUsernameRequest(abort, authHeader, declineTarget.discordUserId, declineTarget.requestedUsername, declineReason)
            .catch(e => processError(e, setRequestsError, navigate))
            .then(() => {
                setProcessing(false);
                setDeclineTarget(undefined);
                setDeclineReason("");
                refresh();
            });
    };

    const dateWithRelative = (ts: string) => (
        <Box>
            <div>{formatDate(ts)}</div>
            <Typography variant="caption" color="text.secondary">
                {formatRelativeTime(ts)}
            </Typography>
        </Box>
    );

    return <FlexBox style={{ flexDirection: 'column' }}>
        <EvosCard variant="outlined">
            <Box component="form" onSubmit={handleSubmit} noValidate style={{ padding: 4 }}>
                <BaseDialog title={code} onDismiss={() => setCode(undefined)} copyTitle />
                <TextField
                    margin="normal"
                    required
                    fullWidth
                    id="issueFor"
                    label="Issue for"
                    name="issueFor"
                    autoFocus
                />
                <Button
                    disabled={processing}
                    type="submit"
                    fullWidth
                    variant="contained"
                    sx={{ mt: 3, mb: 2 }}
                >
                    Issue registration code
                </Button>
                {processing && <LinearProgress />}
            </Box>
        </EvosCard>

        <EvosCard variant="outlined" sx={{ maxWidth: 'none', width: '100%' }}>
            <Typography variant="h6" sx={{ padding: 1 }}>Username requests</Typography>
            {requestsError && <Typography sx={{ padding: 1 }}>
                {`Failed to load requests: ${requestsError.text}${requestsError.description ? `(${requestsError.description})` : ""}`}
            </Typography>}
            {requests && requests.length === 0 && <Typography sx={{ padding: 1 }}>No pending requests.</Typography>}
            {requests && requests.length > 0 &&
                <Table>
                    <TableHead>
                        <TableRow>
                            <TableCell>Requested username</TableCell>
                            <TableCell>Discord user</TableCell>
                            <TableCell>Account created</TableCell>
                            <TableCell>Joined server</TableCell>
                            <TableCell>Requested at</TableCell>
                            <TableCell>Actions</TableCell>
                        </TableRow>
                    </TableHead>
                    <TableBody>
                        {requests.map((row) => (
                            <TableRow key={`${row.discordUserId}:${row.requestedUsername}`}>
                                <TableCell>{row.requestedUsername}</TableCell>
                                <TableCell>
                                    <DiscordUser
                                        displayName={row.discordDisplayName}
                                        userName={row.discordUserName}
                                        userId={row.discordUserId}
                                        avatarUrl={row.discordAvatarUrl}
                                    />
                                </TableCell>
                                <TableCell>{dateWithRelative(row.discordCreatedAt)}</TableCell>
                                <TableCell>{row.discordJoinedAt ? dateWithRelative(row.discordJoinedAt) : "-"}</TableCell>
                                <TableCell>{dateWithRelative(row.requestedAt)}</TableCell>
                                <TableCell>
                                    <Stack direction="row" spacing={1}>
                                        <Button
                                            disabled={processing}
                                            variant="contained"
                                            color="success"
                                            size="small"
                                            onClick={() => handleConfirm(row)}
                                        >
                                            Confirm
                                        </Button>
                                        <Button
                                            disabled={processing}
                                            variant="outlined"
                                            color="error"
                                            size="small"
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
            }
        </EvosCard>

        <Dialog open={declineTarget !== undefined} onClose={() => setDeclineTarget(undefined)} fullWidth>
            <DialogTitle>Decline request for {declineTarget?.requestedUsername}</DialogTitle>
            <DialogContent>
                <DialogContentText>
                    This message will be sent to the requester on Discord.
                </DialogContentText>
                <TextField
                    autoFocus
                    margin="normal"
                    fullWidth
                    multiline
                    label="Reason"
                    value={declineReason}
                    onChange={(e) => setDeclineReason(e.target.value)}
                />
            </DialogContent>
            <DialogActions>
                <Button onClick={() => setDeclineTarget(undefined)}>Cancel</Button>
                <Button color="error" disabled={processing} onClick={submitDecline}>Decline</Button>
            </DialogActions>
        </Dialog>

        {error && <Typography>{`Failed to load codes: ${error.text}${error.description ? `(${error.description})` : ""}`}</Typography>}
        {codes &&
            <Box style={{ margin: "0 auto" }}>
                <Table>
                    <TableHead>
                        <TableRow>
                            <TableCell>Issued to</TableCell>
                            <TableCell>Discord user</TableCell>
                            <TableCell>Issued by</TableCell>
                            <TableCell>Code</TableCell>
                            <TableCell>Issued at</TableCell>
                            <TableCell>Expires at</TableCell>
                        </TableRow>
                    </TableHead>
                    <TableBody>
                        {codes.map((row) => {
                            const claimed= row.issuedTo !== 0;
                            const expired = !claimed && new Date(row.expiresAt) < new Date();
                            return <TableRow
                                key={row.code}
                                sx={{ '&:last-child td, &:last-child th': { border: 0 } }}
                            >
                                <TableCell>{claimed
                                    ? plainAccountLink(row.issuedTo, row.issuedToHandle, navigate)
                                    : row.issuedToHandle}</TableCell>
                                <TableCell>
                                    <DiscordUser
                                        displayName={row.discordDisplayName}
                                        userName={row.discordUserName}
                                        userId={row.discordUserId}
                                        avatarUrl={row.discordAvatarUrl}
                                    />
                                </TableCell>
                                <TableCell>{plainAccountLink(row.issuedBy, row.issuedByHandle, navigate)}</TableCell>
                                <TableCell>{claimed || expired
                                    ? <Tooltip title={claimed ? "Claimed" : "Expired"}><span style={{ textDecoration: "line-through"}}>{row.code}</span></Tooltip>
                                    : <span>{row.code}</span>}
                                </TableCell>
                                <TableCell>{formatDate(row.issuedAt)}</TableCell>
                                <TableCell>{formatDate(row.expiresAt)}</TableCell>
                            </TableRow>
                        })}
                    </TableBody>
                </Table>
            </Box>
        }
    </FlexBox>;
}
