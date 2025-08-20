import React, {useEffect, useState} from 'react';
import {
    Box,
    CircularProgress,
    FormControlLabel,
    Switch,
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableRow,
    TextField,
    Typography
} from '@mui/material';
import {LocalizationProvider} from '@mui/x-date-pickers/LocalizationProvider';
import {AdapterDayjs} from '@mui/x-date-pickers/AdapterDayjs';
import {DateTimePicker} from '@mui/x-date-pickers/DateTimePicker';
import dayjs from 'dayjs';
import {CharacterType, ChatMessage, formatDate, getChatHistory, getPlayers, PlayerData} from "../../lib/Evos";
import {useAuthHeader} from "react-auth-kit";
import {EvosError, processError} from "../../lib/Error";
import {useNavigate} from "react-router-dom";
import ErrorDialog from "../generic/ErrorDialog";
import {EvosCard, FlexBox, plainAccountLink} from "../generic/BasicComponents";
import {CharacterIcon} from "../atlas/CharacterIcon";

interface ChatHistoryProps {
    accountId: number;
}

export const ChatHistory: React.FC<ChatHistoryProps> = ({accountId}: ChatHistoryProps) => {
    const [messages, setMessages] = useState<ChatMessage[]>([]);
    const [players, setPlayers] = useState<Map<number, PlayerData>>(new Map()); // TODO is it needed?
    const [loading, setLoading] = useState(false);
    const [includeBlocked, setIncludeBlocked] = useState(false);
    const [limit, setLimit] = useState(100);
    const [startDate, setStartDate] = useState(dayjs().subtract(7, 'day'));
    const [endDate, setEndDate] = useState(dayjs());
    
    const [error, setError] = useState<EvosError>();
    const authHeader = useAuthHeader()();
    const navigate = useNavigate();

    useEffect(() => {
        if (accountId === 0) {
            setLoading(false);
            return;
        }

        setLoading(true);

        const abort = new AbortController();
        
        const startTimestamp = Math.floor(startDate.unix());
        const endTimestamp = Math.floor(endDate.unix());

        getChatHistory(abort, authHeader, accountId, startTimestamp, endTimestamp, includeBlocked, limit)
            .then((resp) => {
                setMessages(resp.data.messages);

                const accountIds = Array.from(
                    new Set([
                        ...resp.data.messages.map(msg => msg.senderId),
                        ...resp.data.messages.flatMap(msg => msg.recipients)
                    ]));
                return getPlayers(abort, authHeader, accountIds);
            })
            .then((playersResp) => {
                const playersMap = new Map(
                    playersResp.data.players.map(player => [player.player.accountId, player.player])
                );
                setPlayers(playersMap);
            })
            .catch((error) => processError(error, setError, navigate))
            .finally(() => setLoading(false));

        return () => abort.abort();
    }, [accountId, authHeader, includeBlocked, limit, startDate, endDate, navigate]);

    function getBackgroundColor(msg: ChatMessage) {
        return msg.senderId === accountId
            ? msg.recipients.length === 0 && msg.blockedRecipients.length === 0
                ? 'rgba(255, 255, 0, 0.1)'
                : 'rgba(0, 255, 0, 0.1)'
            : msg.recipients.length === 0 && msg.blockedRecipients.length === 0
                ? 'rgba(255, 0, 0, 0.1)'
                : undefined;
    }

    return (
        <FlexBox style={{ flexDirection: 'column' }}>
            { error && <ErrorDialog error={error} onDismiss={() => setError(undefined)} /> }
            <EvosCard variant="outlined">
                <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'center' }}>
                    <LocalizationProvider dateAdapter={AdapterDayjs}>
                        <DateTimePicker
                            label="Start Date"
                            value={startDate}
                            onChange={(newValue) => newValue && setStartDate(newValue)}
                            slotProps={{textField: {size: 'small'}}}
                        />
                        <DateTimePicker
                            label="End Date"
                            value={endDate}
                            onChange={(newValue) => newValue && setEndDate(newValue)}
                            slotProps={{ textField: { size: 'small' } }}
                        />
                    </LocalizationProvider>
                    <TextField
                        type="number"
                        label="Message Limit"
                        value={limit}
                        onChange={(e) => setLimit(Number(e.target.value))}
                        slotProps={{htmlInput: {min: 1, max: 1000}}}
                        size="small"
                    />
                    <FormControlLabel
                        control={
                            <Switch
                                checked={includeBlocked}
                                onChange={(e) => setIncludeBlocked(e.target.checked)}
                            />
                        }
                        label="Include Blocked Messages"
                    />
                </Box>
            </EvosCard>

            { loading &&
                <Box display="flex" justifyContent="center" alignItems="center" minHeight="200px">
                    <CircularProgress/>
                </Box>
            }

            { !loading &&
                <Box style={{ margin: "0 auto" }}>
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
                                <TableCell>Character</TableCell>
                                <TableCell>Player</TableCell>
                                <TableCell>Message</TableCell>
                                <TableCell>Recipients</TableCell>
                                <TableCell>Game</TableCell>
                            </TableRow>
                        </TableHead>
                        <TableBody>
                            {messages.map((msg, index) => (
                                <TableRow
                                    key={index}
                                    sx={{
                                        '&:last-child td, &:last-child th': { border: 0 },
                                        backgroundColor: getBackgroundColor(msg),
                                    }}
                                >
                                    <TableCell sx={{fontSize: "0.6em"}}>{formatDate(msg.time)}</TableCell>
                                    <TableCell>{
                                        msg.character !== CharacterType.None &&
                                        <CharacterIcon
                                            characterType={msg.character}
                                            team={msg.team}
                                            small
                                            noTooltip
                                        />
                                    }</TableCell>
                                    <TableCell>{plainAccountLink(msg.senderId, msg.senderHandle, navigate)}</TableCell>
                                    <TableCell>{msg.message}</TableCell>
                                    <TableCell sx={{fontSize: "0.8em"}}>{[
                                        ...msg.recipients.map(it => plainAccountLink(it, players.get(it)?.handle ?? "UNKNOWN", navigate)),
                                        ...msg.blockedRecipients.map(it => plainAccountLink(it, players.get(it)?.handle ?? "UNKNOWN", navigate, {textDecorationLine: "strikethrough"})),
                                    ].map((element, index, array) => (
                                        <React.Fragment key={index}>
                                            {element}
                                            {index < array.length - 1 && ", "}
                                        </React.Fragment>
                                    ))}</TableCell>

                                    <TableCell>{"TODO"}</TableCell>
                                </TableRow>
                            ))}
                        </TableBody>
                    </Table>
                </Box>
            }

            {!loading && messages.length === 0 && (
                <Typography variant="body1" textAlign="center" mt={2}>
                    No messages found
                </Typography>
            )}
        </FlexBox>
    );
};