import React, {useEffect, useState} from 'react';
import {Box, LinearProgress, Table, TableBody, TableCell, TableHead, TableRow, Typography} from '@mui/material';
import {formatDate, getMatchHistory, MatchHistoryEntry, resultColors, Team} from "../../lib/Evos";
import {useAuthHeader} from "react-auth-kit";
import {EvosError, processError} from "../../lib/Error";
import {useNavigate, useSearchParams} from "react-router-dom";
import ErrorDialog from "../generic/ErrorDialog";
import {FlexBox} from "../generic/BasicComponents";
import {CharacterIcon} from "../atlas/CharacterIcon";
import HistoryNavButtons from "../generic/HistoryNavButtons";
import {useBeforeParamState, useDateParamState} from "../../lib/Lib";

interface MatchHistoryProps {
    accountId: number;
}

const LIMIT = 50;

export const MatchHistory: React.FC<MatchHistoryProps> = ({accountId}: MatchHistoryProps) => {
    const [searchParams, setSearchParams] = useSearchParams();
    const [matches, setMatches] = useState<MatchHistoryEntry[]>([]);
    const [loading, setLoading] = useState(true);

    const [date, setDate] = useDateParamState(searchParams);
    const [isBefore, setIsBefore] = useBeforeParamState(searchParams);

    const [error, setError] = useState<EvosError>();
    const authHeader = useAuthHeader()();
    const navigate = useNavigate();

    useEffect(() => {
        const newParams = new URLSearchParams(searchParams);
        newParams.set('before', isBefore.toString());
        newParams.set('ts', Math.floor(date.unix()).toString());
        setSearchParams(newParams);
    // eslint-disable-next-line
    }, [date, isBefore]);

    useEffect(() => {
        if (accountId === 0) {
            setLoading(false);
            return;
        }

        setLoading(true);
        const abort = new AbortController();
        const timestamp = Math.floor(date.unix());

        getMatchHistory(abort, authHeader, accountId, timestamp, isBefore, LIMIT)
            .then((resp) => setMatches(resp.data.matches))
            .catch((error) => processError(error, setError, navigate))
            .finally(() => setLoading(false));

        return () => abort.abort();
    }, [accountId, authHeader, date, isBefore, navigate]);

    function renderNavigation(withDatePicker: boolean) {
        return <HistoryNavButtons
            items={matches}
            dateFunction={(m: MatchHistoryEntry) => m.matchTime}
            date={date}
            setDate={setDate}
            isBefore={isBefore}
            setIsBefore={setIsBefore}
            disabled={loading}
            datePicker={withDatePicker}
        />;
    }

    return (
        <FlexBox style={{flexDirection: 'column', margin: '1em'}}>
            {error && <ErrorDialog error={error} onDismiss={() => setError(undefined)}/>}
            {renderNavigation(true)}
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
                                <TableCell>Mode</TableCell>
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
                                        backgroundColor: resultColors.get(match.result),
                                        '&:hover': {
                                            backgroundColor: 'rgba(255, 255, 255, 0.08)',
                                            cursor: 'pointer'
                                        }
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
                                    <TableCell>{match.gameType} {match.subType?.split('@')[0]}</TableCell>
                                    <TableCell>{match.mapName}</TableCell>
                                    <TableCell>{match.numOfTurns}</TableCell>
                                    <TableCell>{
                                        match.team === Team.TeamA
                                            ? `${match.teamAScore}-${match.teamBScore}`
                                            : `${match.teamBScore}-${match.teamAScore}`
                                    }</TableCell>
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
            {!loading && matches.length > 0 && renderNavigation(false)}
        </FlexBox>
    );
};