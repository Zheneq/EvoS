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
    Typography
} from '@mui/material';
import {LocalizationProvider} from '@mui/x-date-pickers/LocalizationProvider';
import {AdapterDayjs} from '@mui/x-date-pickers/AdapterDayjs';
import {DateTimePicker} from '@mui/x-date-pickers/DateTimePicker';
import dayjs from 'dayjs';
import {CharacterType, ChatMessage, formatDate, getChatHistory, getPlayers, PlayerData} from "../../lib/Evos";
import {useAuthHeader} from "react-auth-kit";
import {EvosError, processError} from "../../lib/Error";
import {useNavigate, useSearchParams} from "react-router-dom";
import ErrorDialog from "../generic/ErrorDialog";
import {FlexBox, plainAccountLink} from "../generic/BasicComponents";
import {CharacterIcon} from "../atlas/CharacterIcon";

interface ChatHistoryProps {
    accountId: number;
}

export const ChatHistory: React.FC<ChatHistoryProps> = ({accountId}: ChatHistoryProps) => {
    const [searchParams, setSearchParams] = useSearchParams();
    const [messages, setMessages] = useState<ChatMessage[]>([]);
    const [players, setPlayers] = useState<Map<number, PlayerData>>(new Map());
    const [loading, setLoading] = useState(false);

    const defaultStart = dayjs().subtract(1, 'hour');

    const [date, setDate] = useState(() => {
        const startParam = searchParams.get('start');
        return startParam ? dayjs(parseInt(startParam) * 1000) : defaultStart;
    });
    const [isBefore, setIsBefore] = useState(() => {
        const beforeParam = searchParams.get('before');
        return beforeParam === null ? true : beforeParam === 'true';
    });

    const [isWithGeneralChat, setIsWithGeneralChat] = useState(() => {
        const generalChatParam = searchParams.get('generalChat');
        return generalChatParam === null ? true : generalChatParam === 'true';
    });


    const [error, setError] = useState<EvosError>();
    const authHeader = useAuthHeader()();
    const navigate = useNavigate();

    const handleDateChange = (newValue: dayjs.Dayjs | null) => {
        if (newValue) {
            setDate(newValue);
            const newParams = new URLSearchParams(searchParams);
            newParams.set('start', Math.floor(newValue.unix()).toString());
            setSearchParams(newParams);
        }
    };

    const handleBeforeChange = (event: React.ChangeEvent<HTMLInputElement>) => {
        const newValue = event.target.checked;
        setIsBefore(newValue);
        const newParams = new URLSearchParams(searchParams);
        newParams.set('before', newValue.toString());
        setSearchParams(newParams);
    };

    const handleGeneralChatChange = (event: React.ChangeEvent<HTMLInputElement>) => {
        const newValue = event.target.checked;
        setIsWithGeneralChat(newValue);
        const newParams = new URLSearchParams(searchParams);
        newParams.set('generalChat', newValue.toString());
        setSearchParams(newParams);
    };

    useEffect(() => {
        if (accountId === 0) {
            setLoading(false);
            return;
        }

        setLoading(true);

        const abort = new AbortController();
        
        const timestamp = Math.floor(date.unix());

        getChatHistory(abort, authHeader, accountId, timestamp, isBefore, true, isWithGeneralChat)
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
                    playersResp.data.players.map(player => [player.accountId, player])
                );
                setPlayers(playersMap);
            })
            .catch((error) => processError(error, setError, navigate))
            .finally(() => setLoading(false));

        return () => abort.abort();
    }, [accountId, authHeader, date, isBefore, isWithGeneralChat, navigate]);

    function getBackgroundColor(msg: ChatMessage) {
        return msg.recipients.length === 0 && msg.blockedRecipients.length === 0
                ? 'rgba(255, 0, 0, 0.1)'
                : accountId in msg.blockedRecipients
                        ? 'rgba(0, 0, 0, 1)'
                        : undefined;
    }

    function getTextColor(msg: ChatMessage) {
        return msg.senderId === accountId ? 'white' : 'grey.400'
    }

    return (
        <FlexBox style={{ flexDirection: 'column' }}>
            { error && <ErrorDialog error={error} onDismiss={() => setError(undefined)} /> }

            <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'center' }}>
                <LocalizationProvider dateAdapter={AdapterDayjs}>
                    <DateTimePicker
                        label="Start Date"
                        value={date}
                        onChange={handleDateChange}
                        slotProps={{textField: {size: 'small'}}}
                    />
                </LocalizationProvider>
                <FormControlLabel
                    control={
                        <Switch
                            checked={isBefore}
                            onChange={handleBeforeChange}
                            size="small"
                        />
                    }
                    label="Show messages before date"
                />
                <FormControlLabel
                    control={
                        <Switch
                            checked={isWithGeneralChat}
                            onChange={handleGeneralChatChange}
                            size="small"
                        />
                    }
                    label="Include general chat"
                />

            </Box>

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
                                        backgroundColor: getBackgroundColor(msg)
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
                                    <TableCell>{
                                        plainAccountLink(
                                            msg.senderId,
                                            msg.senderHandle,
                                            navigate,
                                            msg.isMuted ? {textDecorationLine: "strikethrough"} : {}
                                        )
                                    }</TableCell>
                                    <TableCell sx={{color: getTextColor(msg)}}>{msg.message}</TableCell>
                                    <TableCell sx={{fontSize: "0.8em"}}>{[
                                        ...msg.recipients.map(it =>
                                            plainAccountLink(
                                                it,
                                                players.get(it)?.handle ?? "UNKNOWN",
                                                navigate
                                            )),
                                        ...msg.blockedRecipients.map(it =>
                                            plainAccountLink(
                                                it,
                                                players.get(it)?.handle ?? "UNKNOWN",
                                                navigate,
                                                {textDecorationLine: "strikethrough"}
                                            )),
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