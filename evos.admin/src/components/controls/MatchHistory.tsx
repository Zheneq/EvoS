import React, {useEffect, useState} from 'react';
import {
    Box,
    Button,
    FormControlLabel,
    LinearProgress,
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
import {formatDate, getMatchHistory, MatchHistoryEntry, Team} from "../../lib/Evos";
import {useAuthHeader} from "react-auth-kit";
import {EvosError, processError} from "../../lib/Error";
import {useNavigate, useSearchParams} from "react-router-dom";
import ErrorDialog from "../generic/ErrorDialog";
import {FlexBox} from "../generic/BasicComponents";
import {CharacterIcon} from "../atlas/CharacterIcon";
import HistoryNavButtons from "../generic/HistoryNavButtons";

interface MatchHistoryProps {
    accountId: number;
}

const LIMIT = 50;

export const MatchHistory: React.FC<MatchHistoryProps> = ({accountId}: MatchHistoryProps) => {
    const [searchParams, setSearchParams] = useSearchParams();
    const [matches, setMatches] = useState<MatchHistoryEntry[]>([]);
    const [loading, setLoading] = useState(false);

    const [date, setDate] = useState(dayjs());
    const [isBefore, setIsBefore] = useState(false);

    const [error, setError] = useState<EvosError>();
    const authHeader = useAuthHeader()();
    const navigate = useNavigate();

    const handleDateChange = (newValue: dayjs.Dayjs | null) => {
        if (newValue) {
            setDate(newValue);
            const newParams = new URLSearchParams(searchParams);
            newParams.set('ts', Math.floor(newValue.unix()).toString());
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

    useEffect(() => {
        if (accountId === 0) {
            setLoading(false);
            return;
        }

        setLoading(true);

        const newParams = new URLSearchParams(searchParams);
        newParams.set('before', isBefore.toString());
        newParams.set('ts', Math.floor(date.unix()).toString());
        setSearchParams(newParams);

        const abort = new AbortController();
        
        const timestamp = Math.floor(date.unix());

        getMatchHistory(abort, authHeader, accountId, timestamp, isBefore, LIMIT)
            .then((resp) => setMatches(resp.data.matches))
            .catch((error) => processError(error, setError, navigate))
            .finally(() => setLoading(false));

        return () => abort.abort();
    }, [accountId, authHeader, date, isBefore, navigate, searchParams, setSearchParams]);

    function renderNavigation() {
        return <HistoryNavButtons
            items={matches}
            dateFunction={(m: MatchHistoryEntry) => m.matchTime}
            setDate={setDate}
            setIsBefore={setIsBefore}
            disabled={loading}
        />;
    }

    return (
        <FlexBox style={{flexDirection: 'column'}}>
            {error && <ErrorDialog error={error} onDismiss={() => setError(undefined)}/>}

            <Box sx={{display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'center'}}>
                <FormControlLabel
                    control={
                        <Switch
                            checked={isBefore}
                            onChange={handleBeforeChange}
                            size="small"
                        />
                    }
                    label={isBefore ? "Showing messages before" : "Showing messages after"}
                />
                <LocalizationProvider dateAdapter={AdapterDayjs}>
                    <DateTimePicker
                        label="Date"
                        value={date}
                        onChange={handleDateChange}
                        slotProps={{textField: {size: 'small'}}}
                    />
                </LocalizationProvider>

            </Box>
            {renderNavigation()}

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
                                <TableCell>Character</TableCell>
                                <TableCell>Map</TableCell>
                                <TableCell>Turns</TableCell>
                                <TableCell>Score</TableCell>
                                <TableCell>Result</TableCell>
                            </TableRow>
                        </TableHead>
                        <TableBody>
                            {matches.toReversed().map((match, index) => (
                                <TableRow
                                    key={index}
                                    sx={{
                                        '&:last-child td, &:last-child th': {border: 0},
                                    }}
                                    onClick={() => navigate(`/account/${accountId}/matches/${match.matchId}`)}
                                >
                                    <TableCell>{formatDate(match.matchTime)}</TableCell>
                                    <TableCell>
                                        <CharacterIcon
                                            characterType={match.character}
                                            team={Team.TeamA}
                                            small
                                            noTooltip
                                        />
                                    </TableCell>
                                    <TableCell>{match.mapName}</TableCell>
                                    <TableCell>{match.numOfTurns}</TableCell>
                                    <TableCell>{match.friendlyScore}-{match.enemyScore}</TableCell>
                                    <TableCell>{match.result}</TableCell>
                                </TableRow>
                            ))}
                        </TableBody>
                    </Table>
                </Box>
            }

            {!loading && matches.length === 0 && (
                <Typography variant="body1" textAlign="center" mt={2}>
                    No matches found
                </Typography>
            )}
            {!loading && matches.length > 0 && renderNavigation()}
        </FlexBox>
    );
};