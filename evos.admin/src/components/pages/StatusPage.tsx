import React, {useMemo, useState} from 'react';
import {getStatus, Status} from "../../lib/Evos";
import {LinearProgress, Typography} from "@mui/material";
import Queue from "../atlas/Queue";
import {useAuthHeader} from "react-auth-kit";
import Server from "../atlas/Server";
import {useNavigate} from "react-router-dom";
import {EvosError, processError} from "../../lib/Error";
import ErrorDialog from "../generic/ErrorDialog";
import useInterval from "../../lib/useInterval";
import useHasFocus from "../../lib/useHasFocus";

function GroupBy<V, K>(key: (item: V) => K, list?: V[]) {
    return list?.reduce((res, p) => {
        res.set(key(p), p);
        return res;
    }, new Map<K, V>())
}

function RoundToNearest5(x: number) {
    return Math.round(x / 5) * 5;
}

function FormatAge(ageMs: number) {
    if (ageMs < 5000) {
        return 'just now';
    }
    if (ageMs < 60000) {
        return `${RoundToNearest5(ageMs / 1000)} seconds ago`;
    }
    if (ageMs < 90000) {
        return `a minute ago`;
    }
    return `${Math.round(ageMs / 60000)} minutes ago`;
}

const UPDATE_PERIOD_MS = 20000;

function StatusPage() {
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<EvosError>();
    const [status, setStatus] = useState<Status>();
    const [updateTime, setUpdateTime] = useState<Date>();
    const [age, setAge] = useState<number>();

    const authHeader = useAuthHeader()();
    const navigate = useNavigate();

    const players = useMemo(() => GroupBy(p => p.accountId, status?.players), [status]);
    const groups = useMemo(() => GroupBy(g => g.groupId, status?.groups), [status]);
    const games = useMemo(() => GroupBy(g => g.server, status?.games), [status]);

    const updatePeriodMs = useHasFocus() || !status ? UPDATE_PERIOD_MS : undefined;

    useInterval(() => {
        getStatus(authHeader)
            .then((resp) => {
                setStatus(resp.data);
                setUpdateTime(new Date());
                setAge(0);
            })
            .catch((error) => processError(error, setError, navigate))
            .then(() => setLoading(false));
    }, updatePeriodMs);

    useInterval(() => {
        if (updateTime) {
            setAge(new Date().getTime() - updateTime.getTime());
        }
    }, 5000);

    const queuedGroups = new Set(status?.queues?.flatMap(q => q.groupIds));
    const notQueuedGroups = groups && [...groups.keys()].filter(g => !queuedGroups.has(g));
    const inGame = games && new Set([...games.values()]
        .flatMap(g => [...g.teamA, ...g.teamB])
        .map(t => t.accountId));

    return (
        <>
            {loading && <LinearProgress />}
            {error && <ErrorDialog error={error} onDismiss={() => setError(undefined)} />}
            <Typography variant={'caption'}>{age === undefined ? 'Loading...' : `Updated ${FormatAge(age)}`}</Typography>
            {status && players && games
                && status.servers
                    .sort((s1, s2) => s1.name.localeCompare(s2.name))
                    .filter(s => games.get(s.id))
                    .map(s => <Server key={s.id} info={s} game={games.get(s.id)} playerData={players}/>)}
            {status && groups && players
                && status.queues.map(q => <Queue key={ `${q.type}_${q.subtype}`} info={q} groupData={groups} playerData={players} />)}
            {notQueuedGroups && groups && players && inGame
                && <Queue
                    key={'not_queued'}
                    info={{type: "Not queued", subtype: "", groupIds: notQueuedGroups}}
                    groupData={groups}
                    playerData={players}
                    hidePlayers={inGame}
                />}
            {status && players && games
                && status.servers
                    .sort((s1, s2) => s1.name.localeCompare(s2.name))
                    .filter(s => !games.get(s.id))
                    .map(s => <Server key={s.id} info={s} game={games.get(s.id)} playerData={players}/>)}
        </>
    );
}

export default StatusPage;
