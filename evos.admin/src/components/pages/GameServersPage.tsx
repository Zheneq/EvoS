import React, {useCallback, useEffect, useState} from 'react';
import {
    Box,
    Button,
    Chip,
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableRow,
    Tooltip,
    Typography,
} from '@mui/material';
import {
    approveGameServerKey,
    declineGameServerKey,
    formatDate,
    GameServerKey,
    getGameServerKeys,
} from '../../lib/Evos';
import {useAuthHeader} from 'react-auth-kit';
import {useNavigate} from 'react-router-dom';
import {EvosError, processError} from '../../lib/Error';
import BaseDialog from '../generic/BaseDialog';
import {FlexBox} from '../generic/BasicComponents';

const REFRESH_MS = 15000;

export default function GameServersPage() {
    const [keys, setKeys] = useState<GameServerKey[]>();
    const [error, setError] = useState<EvosError>();
    const [confirmRevoke, setConfirmRevoke] = useState<GameServerKey>();
    const authHeader = useAuthHeader()();
    const navigate = useNavigate();

    const loadKeys = useCallback(() => {
        const abort = new AbortController();
        getGameServerKeys(abort, authHeader)
            .then(resp => setKeys(resp.data.keys))
            .catch(e => processError(e, setError, navigate));
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

    const handleApprove = (fingerprint: string) => {
        const abort = new AbortController();
        approveGameServerKey(abort, authHeader, fingerprint)
            .then(() => loadKeys())
            .catch(e => processError(e, setError, navigate));
    };

    const handleDecline = (fingerprint: string) => {
        const abort = new AbortController();
        declineGameServerKey(abort, authHeader, fingerprint)
            .then(() => loadKeys())
            .catch(e => processError(e, setError, navigate));
    };

    const doRevoke = () => {
        if (!confirmRevoke) return;
        const fingerprint = confirmRevoke.fingerprint;
        setConfirmRevoke(undefined);
        handleDecline(fingerprint);
    };

    const statusColor = (status: string): 'success' | 'error' | 'warning' | 'default' => {
        if (status === 'Approved') return 'success';
        if (status === 'Pending') return 'warning';
        if (status === 'Revoked' || status === 'Declined') return 'error';
        return 'default';
    };

    const pendingCount = keys?.filter(k => k.status === 'Pending').length ?? 0;

    return (
        <FlexBox style={{flexDirection: 'column'}}>
            <Typography variant="h6" gutterBottom>
                Game servers
                {pendingCount > 0 && (
                    <Chip label={`${pendingCount} pending`} color="warning" size="small" sx={{ml: 1}} />
                )}
            </Typography>
            <Typography variant="body2" color="text.secondary">
                A game server must present an approved key to register. New servers appear here as
                Pending — approve one to let it go live (it connects without a restart).
            </Typography>

            {error && (
                <Typography color="error" sx={{mt: 1}}>
                    {`Error: ${error.text}${error.description ? ` (${error.description})` : ''}`}
                </Typography>
            )}

            <BaseDialog
                title={confirmRevoke ? 'Revoke this key?' : undefined}
                content="This immediately disconnects any server using this key and prevents it from reconnecting until re-approved."
                onDismiss={() => setConfirmRevoke(undefined)}
                onAccept={doRevoke}
                acceptText="Revoke"
            />

            {keys && (
                <Box sx={{mt: 2, overflowX: 'auto'}}>
                    <Table size="small">
                        <TableHead>
                            <TableRow>
                                <TableCell>Name</TableCell>
                                <TableCell>Fingerprint</TableCell>
                                <TableCell>Status</TableCell>
                                <TableCell>Last address</TableCell>
                                <TableCell>Build</TableCell>
                                <TableCell>First seen</TableCell>
                                <TableCell>Last connected</TableCell>
                                <TableCell></TableCell>
                            </TableRow>
                        </TableHead>
                        <TableBody>
                            {keys.map(k => (
                                <TableRow key={k.fingerprint} sx={{'&:last-child td, &:last-child th': {border: 0}}}>
                                    <TableCell>{k.name || <em>unnamed</em>}</TableCell>
                                    <TableCell>
                                        <Tooltip title={k.fingerprint}>
                                            <code>{k.fingerprint.substring(0, 16)}…</code>
                                        </Tooltip>
                                    </TableCell>
                                    <TableCell>
                                        <Chip label={k.status} color={statusColor(k.status)} size="small" />
                                    </TableCell>
                                    <TableCell>{k.lastAddress}</TableCell>
                                    <TableCell>{k.lastBuildVersion}</TableCell>
                                    <TableCell>{formatDate(k.firstSeenAt)}</TableCell>
                                    <TableCell>{k.lastConnectedAt ? formatDate(k.lastConnectedAt) : ''}</TableCell>
                                    <TableCell>
                                        {k.status === 'Pending' && (
                                            <>
                                                <Button
                                                    size="small"
                                                    color="success"
                                                    variant="contained"
                                                    onClick={() => handleApprove(k.fingerprint)}
                                                    sx={{mr: 1}}
                                                >
                                                    Approve
                                                </Button>
                                                <Button
                                                    size="small"
                                                    color="error"
                                                    variant="outlined"
                                                    onClick={() => handleDecline(k.fingerprint)}
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
                                            >
                                                Revoke
                                            </Button>
                                        )}
                                    </TableCell>
                                </TableRow>
                            ))}
                        </TableBody>
                    </Table>
                </Box>
            )}
        </FlexBox>
    );
}
