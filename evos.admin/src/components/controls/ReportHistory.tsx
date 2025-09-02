import React, {useEffect, useState} from 'react';
import {Box, LinearProgress, Table, TableBody, TableCell, TableHead, TableRow, Typography} from '@mui/material';
import {formatDate, getPlayers, getReceivedFeedback, getSentFeedback, PlayerData, UserFeedback} from "../../lib/Evos";
import {useAuthHeader} from "react-auth-kit";
import {EvosError, processError} from "../../lib/Error";
import {useNavigate} from "react-router-dom";
import {FlexBox, plainAccountLink, plainMatchLink, StyledLink} from "../generic/BasicComponents";

interface ReportHistoryProps {
    accountId: number;
    setError: (error: EvosError) => void;
}


export const ReportHistory: React.FC<ReportHistoryProps> = ({accountId, setError}: ReportHistoryProps) => {
    const [receivedFeedback, setReceivedFeedback] = useState<UserFeedback[]>([]);
    const [sentFeedback, setSentFeedback] = useState<UserFeedback[]>([]);
    const [loadingReceived, setLoadingReceived] = useState(true);
    const [loadingSent, setLoadingSent] = useState(true);
    const [loading, setLoading] = useState(true);
    const [players, setPlayers] = useState<Map<number, PlayerData>>(new Map());

    const authHeader = useAuthHeader()();
    const navigate = useNavigate();
    
    useEffect(() => {
        if (accountId === 0) {
            setLoadingReceived(false);
            setLoadingSent(false);
            return;
        }

        setLoading(true);
        setLoadingReceived(true);
        setLoadingSent(true);
        const abort = new AbortController();

        getReceivedFeedback(abort, authHeader, accountId)
            .then((resp) => setReceivedFeedback(resp.data.feedback))
            .catch((error) => processError(error, setError, navigate))
            .finally(() => setLoadingReceived(false));

        getSentFeedback(abort, authHeader, accountId)
            .then((resp) => setSentFeedback(resp.data.feedback))
            .catch((error) => processError(error, setError, navigate))
            .finally(() => setLoadingSent(false));

        return () => abort.abort();
    }, [accountId, authHeader, navigate, setError]);

    useEffect(() => {
        if (loadingReceived || loadingSent || !loading) {
            return;
        }

        const abort = new AbortController();
        const accountIds = Array.from(new Set([...receivedFeedback
            .map(it => it.accountId), accountId]))
            .filter(it => !!it);

        if (accountIds.length === 0) {
            setLoading(false);
            return;
        }

        getPlayers(abort, authHeader, accountIds)
            .then((playersResp) => {
                const playersMap = new Map(
                    playersResp.data.players.map(player => [player.accountId, player])
                );
                setPlayers(playersMap);
                setLoading(false);
            })
            .catch((error) => processError(error, setError, navigate))
    }, [accountId, authHeader, loading, loadingReceived, loadingSent, navigate, receivedFeedback, setError]);
    
    const feedback = [...receivedFeedback, ...sentFeedback]
        .sort((a, b) => new Date(b.time).getTime() - new Date(a.time).getTime());

    function getBackgroundColor(msg: UserFeedback) {
        return msg.reportedPlayerAccountId === accountId ? '#633' : '#363';
    }

    function chatLink(msg: UserFeedback) {
        const ts = Math.floor(new Date(msg.time).getTime() / 1000);
        return `/account/${msg.reportedPlayerAccountId}/chat?before=false&ts=${ts - 300}&limit=${ts + 300}`;
    }

    return (
        <FlexBox style={{flexDirection: 'column', margin: '1em'}}>
            {loading &&
                <Box display="flex" justifyContent="center" alignItems="center" minHeight="200px">
                    <LinearProgress/>
                </Box>
            }
            {!loading &&
                <Box style={{margin: "0 auto"}}>
                    <Table
                        size="small"
                        sx={{
                            '& .MuiTableCell-root': {
                                borderColor: 'grey.800'
                            }
                        }}
                    >
                        <TableHead>
                            <TableRow>
                                <TableCell>Time</TableCell>
                                <TableCell>Context</TableCell>
                                <TableCell>Message</TableCell>
                                <TableCell>Reason</TableCell>
                                <TableCell>From</TableCell>
                                <TableCell>To</TableCell>
                                <TableCell>Chat</TableCell>
                            </TableRow>
                        </TableHead>
                        <TableBody>
                            {feedback.map((msg) => (
                                <TableRow
                                    key={msg.time}
                                    sx={{
                                        '&:last-child td, &:last-child th': {border: 0},
                                        backgroundColor: getBackgroundColor(msg)
                                    }}
                                >
                                    <TableCell>{formatDate(msg.time)}</TableCell>
                                    <TableCell>{msg.context && plainMatchLink(accountId, msg.context, navigate, "Game")}</TableCell>
                                    <TableCell>{msg.message}</TableCell>
                                    <TableCell>{msg.reason}</TableCell>
                                    <TableCell>
                                        {plainAccountLink(msg.accountId, players.get(msg.accountId)?.handle ?? "UNKNWN", navigate)}
                                    </TableCell>
                                    <TableCell>
                                        {plainAccountLink(msg.reportedPlayerAccountId, msg.reportedPlayerHandle, navigate)}
                                    </TableCell>
                                    <TableCell>
                                        <StyledLink target={'_blank'} href={chatLink(msg)}>Chat</StyledLink>
                                    </TableCell>
                                </TableRow>
                            ))}
                        </TableBody>
                    </Table>
                </Box>
            }
            {!loading && feedback.length === 0 && (
                <Typography variant="body1" textAlign="center" mt={2}>
                    No feedback found
                </Typography>
            )}
        </FlexBox>
    );
};