import {
    Box,
    Button,
    Card,
    Chip,
    Divider,
    LinearProgress,
    Stack,
    Table,
    TableBody,
    TableCell,
    TableContainer,
    TableHead,
    TableRow,
    TextField,
    Typography
} from "@mui/material";
import {AdminMessage, sendAdminMessage, formatDate, getAdminMessages} from "../../lib/Evos";
import React, {useEffect, useRef, useState} from "react";
import {useAuthHeader} from "react-auth-kit";
import {useNavigate} from "react-router-dom";
import {EvosError, processError} from "../../lib/Error";
import {plainAccountLink} from "../generic/BasicComponents";
import ErrorDialog from "../generic/ErrorDialog";
import MailRoundedIcon from "@mui/icons-material/MailRounded";
import SendRoundedIcon from "@mui/icons-material/SendRounded";

interface AdminMessagesProps {
    accountId: number;
}

export default function AdminMessages({accountId}: AdminMessagesProps) {
    const [processing, setProcessing] = useState<boolean>();
    const [error, setError] = useState<EvosError>();
    const [messages, setMessages] = useState<AdminMessage[]>();
    const [updateTs, setUpdateTs] = useState<Date>(new Date());
    const formRef = useRef<HTMLFormElement>(null);

    const authHeader = useAuthHeader()();
    const navigate = useNavigate();

    const handleSubmit = (event: React.FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        const data = new FormData(event.currentTarget);
        const msg = data.get('message') as string;

        if (!msg) {
            return;
        }

        setProcessing(true);
        const abort = new AbortController();
        sendAdminMessage(abort, authHeader, accountId, msg)
            .then(() => {
                formRef.current?.reset();
                setUpdateTs(new Date()); // trigger reload
            })
            .catch(e => processError(e, setError, navigate))
            .finally(() => {
                setProcessing(false);
            });

        return () => abort.abort();
    };

    useEffect(() => {
        if (!accountId) return;
        const abort = new AbortController();
        getAdminMessages(abort, authHeader, accountId)
            .then((resp) => {
                setMessages(resp.data.entries);
            })
            .catch((error) => processError(error, setError, navigate));

        return () => abort.abort();
    }, [authHeader, navigate, accountId, updateTs]);

    return (
        <Card variant="outlined">
            {error && <ErrorDialog error={error} onDismiss={() => setError(undefined)} />}

            <Stack spacing={2.5} sx={{ width: '100%' }}>
                <Box>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                        <MailRoundedIcon color="primary" />
                        <Typography variant="h6" sx={{ fontWeight: 600 }}>
                            Admin Direct Messages
                        </Typography>
                    </Box>
                    <Typography variant="body2" color="text.secondary">
                        Send official administrative notices directly to this player's inbox.
                    </Typography>
                </Box>

                <Box component="form" ref={formRef} onSubmit={handleSubmit} noValidate sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
                    <TextField
                        size="small"
                        required
                        fullWidth
                        id="message"
                        label="Message content"
                        name="message"
                        multiline
                        rows={2}
                        placeholder="Type notification message..."
                    />
                    <Box sx={{ display: 'flex', justifyContent: 'flex-end' }}>
                        <Button
                            disabled={processing || !accountId}
                            type="submit"
                            variant="contained"
                            color="primary"
                            startIcon={<SendRoundedIcon />}
                        >
                            Send Notice
                        </Button>
                    </Box>
                    {processing && <LinearProgress sx={{ borderRadius: 1 }} />}
                </Box>

                <Divider />

                <Box>
                    <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 1.5 }}>
                        <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
                            Message History
                        </Typography>
                        <Chip
                            label={`${messages?.length ?? 0} message${(messages?.length ?? 0) === 1 ? '' : 's'}`}
                            size="small"
                            variant="outlined"
                        />
                    </Box>

                    {messages && messages.length > 0 ? (
                        <TableContainer sx={{ maxHeight: 320, borderRadius: 1.5, border: '1px solid', borderColor: 'divider' }}>
                            <Table size="small" stickyHeader>
                                <TableHead>
                                    <TableRow>
                                        <TableCell sx={{ fontWeight: 600 }}>From</TableCell>
                                        <TableCell sx={{ fontWeight: 600 }}>Message</TableCell>
                                        <TableCell sx={{ fontWeight: 600 }}>Sent At</TableCell>
                                        <TableCell sx={{ fontWeight: 600 }}>Viewed At</TableCell>
                                        <TableCell sx={{ fontWeight: 600 }}>Status</TableCell>
                                    </TableRow>
                                </TableHead>
                                <TableBody>
                                    {messages.map((row) => (
                                        <TableRow
                                            key={row.sentAt}
                                            hover
                                            sx={{ '&:last-child td, &:last-child th': { border: 0 } }}
                                        >
                                            <TableCell>{plainAccountLink(row.from, row.fromHandle, navigate)}</TableCell>
                                            <TableCell sx={{ maxWidth: 300, wordBreak: 'break-word' }}>{row.text}</TableCell>
                                            <TableCell>{formatDate(row.sentAt)}</TableCell>
                                            <TableCell>{row.viewedAt ? formatDate(row.viewedAt) : '—'}</TableCell>
                                            <TableCell>
                                                <Chip
                                                    label={row.viewedAt ? "Read" : "Unread"}
                                                    color={row.viewedAt ? "success" : "default"}
                                                    size="small"
                                                    variant={row.viewedAt ? "filled" : "outlined"}
                                                />
                                            </TableCell>
                                        </TableRow>
                                    ))}
                                </TableBody>
                            </Table>
                        </TableContainer>
                    ) : (
                        <Typography variant="body2" color="text.secondary" sx={{ py: 2, textAlign: 'center' }}>
                            No messages have been sent to this account yet.
                        </Typography>
                    )}
                </Box>
            </Stack>
        </Card>
    );
}