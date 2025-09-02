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
import dayjs from 'dayjs';
import {
    CharacterType,
    ChatMessage,
    chatTypeColors,
    formatDate,
    getChatHistory,
    getPlayers,
    PlayerData
} from "../../lib/Evos";
import {useAuthHeader} from "react-auth-kit";
import {EvosError, processError} from "../../lib/Error";
import {useNavigate, useSearchParams} from "react-router-dom";
import ErrorDialog from "../generic/ErrorDialog";
import {FlexBox, plainAccountLink, plainMatchLink} from "../generic/BasicComponents";
import {CharacterIcon} from "../atlas/CharacterIcon";
import HistoryNavButtons from "../generic/HistoryNavButtons";

interface ChatHistoryProps {
    accountId: number;
}

const LIMIT = 50;

export const ChatHistory: React.FC<ChatHistoryProps> = ({accountId}: ChatHistoryProps) => {
    const [searchParams, setSearchParams] = useSearchParams();
    const [messages, setMessages] = useState<ChatMessage[]>([]);
    const [players, setPlayers] = useState<Map<number, PlayerData>>(new Map());
    const [loading, setLoading] = useState(true);

    const [date, setDate] = useState(() => {
        const tsParam = searchParams.get('ts');
        return tsParam ? dayjs(parseInt(tsParam) * 1000) : dayjs();
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
    }, [date, isBefore, isWithGeneralChat]);

    useEffect(() => {
        if (accountId === 0) {
            setLoading(false);
            return;
        }

        setLoading(true);

        const abort = new AbortController();
        
        const timestamp = Math.floor(date.unix());

        getChatHistory(abort, authHeader, accountId, timestamp, isBefore, true, isWithGeneralChat, LIMIT)
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

        return () => abort.abort()
    }, [accountId, authHeader, date, isBefore, isWithGeneralChat, navigate]);

    function getBackgroundColor(msg: ChatMessage) {
        return chatTypeColors.get(msg.type) ?? 'rgba(0,0,0,0)';
    }

    function getTextColor(msg: ChatMessage) {
        return msg.senderId === accountId ? 'white' : 'grey.400'
    }

    function renderNavigation(withDatePicker: boolean) {
        return <HistoryNavButtons
            items={messages}
            dateFunction={(m: ChatMessage) => m.time}
            date={date}
            setDate={setDate}
            isBefore={isBefore}
            setIsBefore={setIsBefore}
            disabled={loading}
            datePicker={withDatePicker}
        />;
    }

    return (
        <FlexBox style={{flexDirection: 'column'}}>
            {error && <ErrorDialog error={error} onDismiss={() => setError(undefined)}/>}

            <Box sx={{display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'center'}}>
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
            {renderNavigation(true)}

            {loading &&
                <Box display="flex" justifyContent="center" alignItems="center" minHeight="200px">
                    <CircularProgress/>
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
                                        '&:last-child td, &:last-child th': {border: 0},
                                        backgroundColor: getBackgroundColor(msg)
                                    }}
                                    title={msg.type}
                                >
                                    <TableCell sx={{fontSize: "0.6em"}}>{formatDate(msg.time)}</TableCell>
                                    <TableCell>{
                                        msg.game !== null &&
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
                                    <TableCell sx={{
                                        color: getTextColor(msg),
                                        textDecorationLine: msg.isMuted || accountId in msg.blockedRecipients ? "strikethrough" : "none"
                                    }}>
                                        {msg.message}
                                    </TableCell>
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

                                    <TableCell>{msg.game && plainMatchLink(accountId, msg.game, navigate)}</TableCell>
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
            {!loading && messages.length > 0 && renderNavigation(false)}
        </FlexBox>
    );
};