import React, {useEffect, useState} from 'react';
import {useAuthHeader} from 'react-auth-kit';
import {useNavigate, useParams} from 'react-router-dom';
import {getMatch, getPlayers, MatchData, PlayerData} from '../../lib/Evos';
import {Container, LinearProgress, Paper, Typography} from '@mui/material';
import {FlexBox} from '../generic/BasicComponents';
import {Match} from "../atlas/Match";
import {EvosError, processError} from "../../lib/Error";
import ErrorDialog from "../generic/ErrorDialog";

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
            setLoading(false);
            return;
        }

        setLoading(true);

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

    return (
        <FlexBox style={{flexDirection: 'column'}}>
            {error && <ErrorDialog error={error} onDismiss={() => setError(undefined)}/>}
            <Container>
                <Paper>
                    {match
                        ? <Match match={match} playerData={players} />
                        : loading
                            ? <LinearProgress />
                            : <Typography>No match data found</Typography>
                    }
                </Paper>
            </Container>
        </FlexBox>
    );
}