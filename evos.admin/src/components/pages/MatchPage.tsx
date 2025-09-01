import { useEffect, useState } from 'react';
import { useAuthHeader } from 'react-auth-kit';
import {useNavigate, useParams} from 'react-router-dom';
import {getMatch, getPlayers, MatchData, PlayerData} from '../../lib/Evos';
import {
    CircularProgress,
    Container,
    Paper,
    Typography,
    Grid,
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableRow
} from '@mui/material';
import { EvosCard } from '../generic/BasicComponents';
import {Match} from "../atlas/Match";
import {EvosError, processError} from "../../lib/Error";

export default function MatchPage() {
    const { accountId, matchId } = useParams();
    
    const [match, setMatch] = useState<MatchData | null>(null);
    const [players, setPlayers] = useState<Map<number, PlayerData>>(new Map());
    const [loading, setLoading] = useState(true);

    const [error, setError] = useState<EvosError>();
    const authHeader = useAuthHeader()();
    const navigate = useNavigate();

    useEffect(() => {
        const abort = new AbortController();

        if (!accountId || !matchId) {
            setError({text: 'Account ID or Match ID not provided'});
            return;
        }
        getMatch(abort, authHeader, parseInt(accountId), matchId)
            .then((resp) => {
                setMatch(resp.data);

                const accountIds = Array.from(
                    new Set([
                        ...resp.data.matchDetailsComponent.matchResults.friendlyTeamStats.map(s => s.player.accountId),
                        ...resp.data.matchDetailsComponent.matchResults.enemyTeamStats.map(s => s.player.accountId),
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

        return () => {
            abort.abort();
        };
    }, [accountId, matchId, authHeader, navigate]);



    if (loading) {
        return (
            <Container sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}>
                <CircularProgress />
            </Container>
        );
    }


    // TODO error popup
    // if (error) {
    //     return (
    //         <Container>
    //             <Paper sx={{ p: 2, mt: 2 }}>
    //                 <Typography color="error">{`Error: ${error.text}`}</Typography>
    //             </Paper>
    //         </Container>
    //     );
    // }

    if (!match) {
        return (
            <Container>
                <Paper sx={{ p: 2, mt: 2 }}>
                    <Typography>No match data found</Typography>
                </Paper>
            </Container>
        );
    }

    return (
        <Container>
            <Paper>
                <Match match={match} playerData={players} />
            </Paper>
        </Container>
    );
}