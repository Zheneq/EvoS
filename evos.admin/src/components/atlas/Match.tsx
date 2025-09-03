import React from 'react';
import {
    Box,
    Grid,
    Paper,
    Stack,
    Table,
    TableBody,
    TableCell,
    TableContainer,
    TableHead,
    TableRow,
    Tooltip,
    Typography,
} from '@mui/material';
import {GiBroadsword, GiDeathSkull, GiHealthNormal} from 'react-icons/gi';
import {BsShield} from 'react-icons/bs';
import {PiSwordDuotone, PiSwordFill} from 'react-icons/pi';
import Player from '../atlas/Player';
import {formatDate, MatchData, PlayerData, PlayerGameResult, Team, TeamStatline} from "../../lib/Evos";
import {CharacterIcon} from "./CharacterIcon";

const getPovTeam: (match: MatchData) => Team = (match: MatchData) => {
    return match.matchComponent.participants.find((player) => player.isPlayer)!!.team
}

interface TeamData {
    team: Team;
    players: TeamStatline[];
}

const getTeams: (match: MatchData) => TeamData[] | undefined = (match: MatchData) => {
    if (getPovTeam(match) === Team.TeamA) {
        return [
            {team: Team.TeamA, players: match.matchDetailsComponent.matchResults.friendlyTeamStats} as TeamData,
            {team: Team.TeamB, players: match.matchDetailsComponent.matchResults.enemyTeamStats} as TeamData,
        ]
    } else if (getPovTeam(match) === Team.TeamB) {
        return [
            {team: Team.TeamA, players: match.matchDetailsComponent.matchResults.enemyTeamStats} as TeamData,
            {team: Team.TeamB, players: match.matchDetailsComponent.matchResults.friendlyTeamStats} as TeamData,
        ]
    }
}

interface MatchProps {
    match: MatchData;
    playerData: Map<number, PlayerData>;
}

export const Match: React.FC<MatchProps> = ({match, playerData}: MatchProps) => {
    const povTeam = getPovTeam(match);
    const otherTeam = povTeam === Team.TeamA ? Team.TeamB : Team.TeamA;
    const score = `${match.matchDetailsComponent.matchResults.score.teamAScore}-${match.matchDetailsComponent.matchResults.score.teamBScore}`

    return (
        <Box>
            <Grid container spacing={2} sx={{padding: '1em'}}>
                <Grid>
                    <Typography variant="subtitle1" gutterBottom>
                        {`Score: ${score}`}
                    </Typography>
                </Grid>
                <Grid>
                    <Typography variant="subtitle1" gutterBottom>
                        {`Turns: ${match.matchComponent.turnsPlayed}`}
                    </Typography>
                </Grid>
                <Grid>
                    <Typography variant="subtitle1" gutterBottom>
                        {`Date: ${formatDate(match.createDate)}`}
                    </Typography>
                </Grid>
                <Grid>
                    <Typography variant="subtitle1" gutterBottom>
                        {`Type: ${match.matchComponent.gameType}`}
                    </Typography>
                </Grid>
                <Grid>
                    <Typography variant="subtitle1" gutterBottom>
                        {`Map: ${match.matchComponent.mapName}`}
                    </Typography>
                </Grid>
                <Grid>
                    <Typography variant="subtitle1" gutterBottom>
                        {`${match.gameServerProcessCode} (vTODO)`}
                    </Typography>
                </Grid>
            </Grid>
            <Box display="flex" flexDirection="column">
                {getTeams(match)?.map(({team, players}: TeamData) => (
                    <TableContainer
                        key={team}
                        component={Paper}
                        sx={{
                            marginBottom: '1em',
                        }}
                    >
                        <Table size="small" aria-label="player stats">
                            <TableHead>
                                <TableRow>
                                    <TableCell></TableCell>
                                    <TableCell><Tooltip title="Takedowns"><div><PiSwordDuotone/></div></Tooltip></TableCell>
                                    <TableCell><Tooltip title="Deaths"><div><GiDeathSkull/></div></Tooltip></TableCell>
                                    <TableCell><Tooltip title="Deathblows"><div><PiSwordFill/></div></Tooltip></TableCell>
                                    <TableCell><Tooltip title="Damage"><div><GiBroadsword/></div></Tooltip></TableCell>
                                    <TableCell><Tooltip title="Healing"><div><GiHealthNormal/></div></Tooltip></TableCell>
                                    <TableCell><Tooltip title="Damage received"><div><BsShield/></div></Tooltip></TableCell>
                                </TableRow>
                            </TableHead>
                            <TableBody>
                                {players.map((player: TeamStatline) => {
                                    const info = playerData.get(player.player.accountId); // TODO use PlayerCustomization
                                    return (
                                        <TableRow
                                            key={player.player.playerId}
                                            sx={{
                                                marginBottom: '1em',
                                                backgroundColor:
                                                    (match.matchComponent.result === PlayerGameResult.Win && team === povTeam)
                                                        || (match.matchComponent.result === PlayerGameResult.Lose && team !== povTeam)
                                                        ? '#22c9554f'
                                                        : '#ff423a4f',
                                            }}
                                        >
                                            <TableCell>
                                                <Stack direction={'row'}>
                                                    <Player info={info} bot={player.player.accountId === 0} />
                                                    <CharacterIcon
                                                        characterType={player.character.type}
                                                        data={info}
                                                        team={player.player.isAlly ? povTeam : otherTeam}
                                                        rightSkew
                                                        noTooltip
                                                    />
                                                </Stack>
                                            </TableCell>
                                            <TableCell>{player.combatStats.kills}</TableCell>
                                            <TableCell>{player.combatStats.deaths}</TableCell>
                                            <TableCell>{player.combatStats.assists}</TableCell>
                                            <TableCell>{player.combatStats.damageDealt}</TableCell>
                                            <TableCell>{player.combatStats.healing}</TableCell>
                                            <TableCell>{player.combatStats.damageTaken}</TableCell>
                                        </TableRow>
                                    );
                                })}
                            </TableBody>
                        </Table>
                    </TableContainer>
                ))}
            </Box>
        </Box>
    );
}
