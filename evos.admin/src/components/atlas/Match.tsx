import React, {useEffect, useState} from 'react';
import {
    Box,
    Chip,
    Grid,
    Paper, Stack,
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
// import {FaRankingStar} from 'react-icons/fa6';
// import {denydNames, PlayerData} from 'renderer/lib/Evos';
// import {ability, abilityIcon, catalystsIcon} from 'renderer/lib/Resources';
import Player from '../atlas/Player';
import {formatDate, MatchActor, MatchData, PlayerData, PlayerGameResult, Team, TeamStatline} from "../../lib/Evos";
import {useNavigate} from "react-router-dom";
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
    const game = match

    return (
        <Box>
            <Grid container spacing={2} sx={{padding: '1em'}}>
                <Grid>
                    <Typography variant="subtitle1" gutterBottom>
                        {`Score: ${game.matchDetailsComponent.matchResults.score.blueTeam}-${game.matchDetailsComponent.matchResults.score.redTeam}`}
                    </Typography>
                </Grid>
                <Grid>
                    <Typography variant="subtitle1" gutterBottom>
                        {`Turns: ${game.matchComponent.turnsPlayed}`}
                    </Typography>
                </Grid>
                <Grid>
                    <Typography variant="subtitle1" gutterBottom>
                        {`Date: ${formatDate(game.createDate)}`}
                    </Typography>
                </Grid>
                <Grid>
                    <Typography variant="subtitle1" gutterBottom>
                        {`Type: ${game.matchComponent.gameType}`}
                    </Typography>
                </Grid>
                <Grid>
                    <Typography variant="subtitle1" gutterBottom>
                        {`${game.gameServerProcessCode} (vTODO)`}
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
                                    <TableCell>Player</TableCell>
                                    <TableCell>Badges</TableCell>
                                    <TableCell>
                                        <Tooltip title="Takedowns">
                                            <div>
                                                <PiSwordDuotone/>
                                            </div>
                                        </Tooltip>
                                    </TableCell>
                                    <TableCell>
                                        <Tooltip title="Deaths">
                                            <div>
                                                <GiDeathSkull/>
                                            </div>
                                        </Tooltip>
                                    </TableCell>
                                    <TableCell>
                                        <Tooltip title="Deathblows">
                                            <div>
                                                <PiSwordFill/>
                                            </div>
                                        </Tooltip>
                                    </TableCell>
                                    <TableCell>
                                        <Tooltip title="Damage">
                                            <div>
                                                <GiBroadsword/>
                                            </div>
                                        </Tooltip>
                                    </TableCell>
                                    <TableCell>
                                        <Tooltip title="Healing">
                                            <div>
                                                <GiHealthNormal/>
                                            </div>
                                        </Tooltip>
                                    </TableCell>
                                    <TableCell>
                                        <Tooltip title="Damage received">
                                            <div>
                                                <BsShield/>
                                            </div>
                                        </Tooltip>
                                    </TableCell>
                                </TableRow>
                            </TableHead>
                            <TableBody>
                                {players.map((player: TeamStatline) => {
                                    const info = playerData.get(player.player.accountId); // TODO use PlayerCustomization
                                    return (
                                        <TableRow
                                            key={player.player.accountId}
                                            sx={{
                                                marginBottom: '1em',
                                                backgroundColor:
                                                    game.matchComponent.result === PlayerGameResult.Win // TODO fix
                                                        ? '#22c9554f'
                                                        : '#ff423a4f',
                                            }}
                                        >
                                            <TableCell>
                                                <div style={{display: 'flex', alignItems: 'center'}}>
                                                    <div>
                                                        <Stack direction={'row'}>
                                                            <Player info={info} />
                                                            <CharacterIcon
                                                                characterType={player.character.type}
                                                                data={info}
                                                                team={player.player.isAlly ? getPovTeam(match) : getPovTeam(match) == Team.TeamB ? Team.TeamA : Team.TeamB}
                                                                rightSkew
                                                                noTooltip
                                                            />
                                                        </Stack>
                                                    </div>
                                                    <div style={{marginLeft: 'auto'}}>
                                                        {/*{calculateMVPBadge(player, game.stats) &&*/}
                                                        {/*    !customPlayers && (*/}
                                                        {/*        <Tooltip title={t('stats.mvp')}>*/}
                                                        {/*            <Chip*/}
                                                        {/*                color="primary"*/}
                                                        {/*                label=""*/}
                                                        {/*                size="small"*/}
                                                        {/*                icon={*/}
                                                        {/*                    <FaRankingStar*/}
                                                        {/*                        style={{marginLeft: '12px'}}*/}
                                                        {/*                    />*/}
                                                        {/*                }*/}
                                                        {/*                sx={{marginLeft: 1}} // Add margin to badges*/}
                                                        {/*            />*/}
                                                        {/*        </Tooltip>*/}
                                                        {/*    )}*/}
                                                        {/*{calculateHealerBadge(player, game.stats) &&*/}
                                                        {/*    !customPlayers && (*/}
                                                        {/*        <Tooltip title={t('stats.bestSupport')}>*/}
                                                        {/*            <Chip*/}
                                                        {/*                color="secondary"*/}
                                                        {/*                label=""*/}
                                                        {/*                size="small"*/}
                                                        {/*                icon={*/}
                                                        {/*                    <GiHealthNormal*/}
                                                        {/*                        style={{marginLeft: '12px'}}*/}
                                                        {/*                    />*/}
                                                        {/*                }*/}
                                                        {/*                sx={{marginLeft: 1}} // Add margin to badges*/}
                                                        {/*            />*/}
                                                        {/*        </Tooltip>*/}
                                                        {/*    )}*/}
                                                        {/*{calculateDamageBadge(player, game.stats) &&*/}
                                                        {/*    !customPlayers && (*/}
                                                        {/*        <Tooltip title={t('stats.bestDamage')}>*/}
                                                        {/*            <Chip*/}
                                                        {/*                color="error"*/}
                                                        {/*                label=""*/}
                                                        {/*                size="small"*/}
                                                        {/*                icon={*/}
                                                        {/*                    <GiBroadsword*/}
                                                        {/*                        style={{marginLeft: '12px'}}*/}
                                                        {/*                    />*/}
                                                        {/*                }*/}
                                                        {/*                sx={{marginLeft: 1}} // Add margin to badges*/}
                                                        {/*            />*/}
                                                        {/*        </Tooltip>*/}
                                                        {/*    )}*/}
                                                        {/*{calculateTankBadge(player, game.stats) &&*/}
                                                        {/*    !customPlayers && (*/}
                                                        {/*        <Tooltip title={t('stats.bestTank')}>*/}
                                                        {/*            <Chip*/}
                                                        {/*                color="info"*/}
                                                        {/*                label=""*/}
                                                        {/*                size="small"*/}
                                                        {/*                icon={*/}
                                                        {/*                    <BsShield*/}
                                                        {/*                        style={{marginLeft: '12px'}}*/}
                                                        {/*                    />*/}
                                                        {/*                }*/}
                                                        {/*                sx={{marginLeft: 1}} // Add margin to badges*/}
                                                        {/*            />*/}
                                                        {/*        </Tooltip>*/}
                                                        {/*    )}*/}
                                                    </div>
                                                </div>
                                            </TableCell>
                                            <TableCell>
                                                {/*{t(`charNames.${player.character.replace(/:3/g, '')}`)}*/}
                                                {/*{customPlayers &&*/}
                                                {/*    customPlayers.map((customPlayer) => {*/}
                                                {/*        // Check if this handle has already been rendered*/}
                                                {/*        if (*/}
                                                {/*            !renderedHandles.has(customPlayer.Handle) &&*/}
                                                {/*            customPlayer.Handle === player.user*/}
                                                {/*        ) {*/}
                                                {/*            // If not rendered yet and matches the current player's handle, render the content*/}
                                                {/*            renderedHandles.add(customPlayer.Handle); // Mark this handle as rendered*/}
                                                {/*            const tooltip1 = ability(*/}
                                                {/*                player.character.replace(/:3/g, ''),*/}
                                                {/*                0,*/}
                                                {/*                customPlayer.CharacterInfo.CharacterMods*/}
                                                {/*                    .ModForAbility0,*/}
                                                {/*            );*/}
                                                {/*            const tooltip2 = ability(*/}
                                                {/*                player.character.replace(/:3/g, ''),*/}
                                                {/*                1,*/}
                                                {/*                customPlayer.CharacterInfo.CharacterMods*/}
                                                {/*                    .ModForAbility1,*/}
                                                {/*            );*/}
                                                {/*            const tooltip3 = ability(*/}
                                                {/*                player.character.replace(/:3/g, ''),*/}
                                                {/*                2,*/}
                                                {/*                customPlayer.CharacterInfo.CharacterMods*/}
                                                {/*                    .ModForAbility2,*/}
                                                {/*            );*/}
                                                {/*            const tooltip4 = ability(*/}
                                                {/*                player.character.replace(/:3/g, ''),*/}
                                                {/*                3,*/}
                                                {/*                customPlayer.CharacterInfo.CharacterMods*/}
                                                {/*                    .ModForAbility3,*/}
                                                {/*            );*/}
                                                {/*            const tooltip5 = ability(*/}
                                                {/*                player.character.replace(/:3/g, ''),*/}
                                                {/*                4,*/}
                                                {/*                customPlayer.CharacterInfo.CharacterMods*/}
                                                {/*                    .ModForAbility4,*/}
                                                {/*            );*/}

                                                {/*            return (*/}
                                                {/*                <div key={customPlayer.Handle}>*/}
                                                {/*                    <Typography variant="caption">*/}
                                                {/*                        <Tooltip*/}
                                                {/*                            arrow*/}
                                                {/*                            placement="top"*/}
                                                {/*                            title={*/}
                                                {/*                                <>*/}
                                                {/*                                    <Typography*/}
                                                {/*                                        variant="h5"*/}
                                                {/*                                        component="div"*/}
                                                {/*                                    >*/}
                                                {/*                                        <div*/}
                                                {/*                                            dangerouslySetInnerHTML={{*/}
                                                {/*                                                __html: tooltip1.title,*/}
                                                {/*                                            }}*/}
                                                {/*                                        />*/}
                                                {/*                                    </Typography>*/}
                                                {/*                                    <Typography*/}
                                                {/*                                        variant="body1"*/}
                                                {/*                                        component="div"*/}
                                                {/*                                    >*/}
                                                {/*                                        <div*/}
                                                {/*                                            dangerouslySetInnerHTML={{*/}
                                                {/*                                                __html: tooltip1.tooltip,*/}
                                                {/*                                            }}*/}
                                                {/*                                        />*/}
                                                {/*                                    </Typography>*/}
                                                {/*                                </>*/}
                                                {/*                            }*/}
                                                {/*                        >*/}
                                                {/*                            <img*/}
                                                {/*                                src={abilityIcon(*/}
                                                {/*                                    player.character.replace(/:3/g, ''),*/}
                                                {/*                                    1,*/}
                                                {/*                                )}*/}
                                                {/*                                alt="ability1"*/}
                                                {/*                                width={25}*/}
                                                {/*                                height={25}*/}
                                                {/*                                style={{cursor: 'pointer'}}*/}
                                                {/*                            />*/}
                                                {/*                        </Tooltip>*/}
                                                {/*                    </Typography>*/}
                                                {/*                    <Typography variant="caption">*/}
                                                {/*                        <Tooltip*/}
                                                {/*                            arrow*/}
                                                {/*                            placement="top"*/}
                                                {/*                            title={*/}
                                                {/*                                <>*/}
                                                {/*                                    <Typography*/}
                                                {/*                                        variant="h5"*/}
                                                {/*                                        component="div"*/}
                                                {/*                                    >*/}
                                                {/*                                        <div*/}
                                                {/*                                            dangerouslySetInnerHTML={{*/}
                                                {/*                                                __html: tooltip2.title,*/}
                                                {/*                                            }}*/}
                                                {/*                                        />*/}
                                                {/*                                    </Typography>*/}
                                                {/*                                    <Typography*/}
                                                {/*                                        variant="body1"*/}
                                                {/*                                        component="div"*/}
                                                {/*                                    >*/}
                                                {/*                                        <div*/}
                                                {/*                                            dangerouslySetInnerHTML={{*/}
                                                {/*                                                __html: tooltip2.tooltip,*/}
                                                {/*                                            }}*/}
                                                {/*                                        />*/}
                                                {/*                                    </Typography>*/}
                                                {/*                                </>*/}
                                                {/*                            }*/}
                                                {/*                        >*/}
                                                {/*                            <img*/}
                                                {/*                                src={abilityIcon(*/}
                                                {/*                                    player.character.replace(/:3/g, ''),*/}
                                                {/*                                    2,*/}
                                                {/*                                )}*/}
                                                {/*                                alt="ability2"*/}
                                                {/*                                width={25}*/}
                                                {/*                                height={25}*/}
                                                {/*                                style={{cursor: 'pointer'}}*/}
                                                {/*                            />*/}
                                                {/*                        </Tooltip>*/}
                                                {/*                    </Typography>*/}
                                                {/*                    <Typography variant="caption">*/}
                                                {/*                        <Tooltip*/}
                                                {/*                            arrow*/}
                                                {/*                            placement="top"*/}
                                                {/*                            title={*/}
                                                {/*                                <>*/}
                                                {/*                                    <Typography*/}
                                                {/*                                        variant="h5"*/}
                                                {/*                                        component="div"*/}
                                                {/*                                    >*/}
                                                {/*                                        <div*/}
                                                {/*                                            dangerouslySetInnerHTML={{*/}
                                                {/*                                                __html: tooltip3.title,*/}
                                                {/*                                            }}*/}
                                                {/*                                        />*/}
                                                {/*                                    </Typography>*/}
                                                {/*                                    <Typography*/}
                                                {/*                                        variant="body1"*/}
                                                {/*                                        component="div"*/}
                                                {/*                                    >*/}
                                                {/*                                        <div*/}
                                                {/*                                            dangerouslySetInnerHTML={{*/}
                                                {/*                                                __html: tooltip3.tooltip,*/}
                                                {/*                                            }}*/}
                                                {/*                                        />*/}
                                                {/*                                    </Typography>*/}
                                                {/*                                </>*/}
                                                {/*                            }*/}
                                                {/*                        >*/}
                                                {/*                            <img*/}
                                                {/*                                src={abilityIcon(*/}
                                                {/*                                    player.character.replace(/:3/g, ''),*/}
                                                {/*                                    3,*/}
                                                {/*                                )}*/}
                                                {/*                                alt="ability3"*/}
                                                {/*                                width={25}*/}
                                                {/*                                height={25}*/}
                                                {/*                                style={{cursor: 'pointer'}}*/}
                                                {/*                            />*/}
                                                {/*                        </Tooltip>*/}
                                                {/*                    </Typography>*/}
                                                {/*                    <Typography variant="caption">*/}
                                                {/*                        <Tooltip*/}
                                                {/*                            arrow*/}
                                                {/*                            placement="top"*/}
                                                {/*                            title={*/}
                                                {/*                                <>*/}
                                                {/*                                    <Typography*/}
                                                {/*                                        variant="h5"*/}
                                                {/*                                        component="div"*/}
                                                {/*                                    >*/}
                                                {/*                                        <div*/}
                                                {/*                                            dangerouslySetInnerHTML={{*/}
                                                {/*                                                __html: tooltip4.title,*/}
                                                {/*                                            }}*/}
                                                {/*                                        />*/}
                                                {/*                                    </Typography>*/}
                                                {/*                                    <Typography*/}
                                                {/*                                        variant="body1"*/}
                                                {/*                                        component="div"*/}
                                                {/*                                    >*/}
                                                {/*                                        <div*/}
                                                {/*                                            dangerouslySetInnerHTML={{*/}
                                                {/*                                                __html: tooltip4.tooltip,*/}
                                                {/*                                            }}*/}
                                                {/*                                        />*/}
                                                {/*                                    </Typography>*/}
                                                {/*                                </>*/}
                                                {/*                            }*/}
                                                {/*                        >*/}
                                                {/*                            <img*/}
                                                {/*                                src={abilityIcon(*/}
                                                {/*                                    player.character.replace(/:3/g, ''),*/}
                                                {/*                                    4,*/}
                                                {/*                                )}*/}
                                                {/*                                alt="ability4"*/}
                                                {/*                                width={25}*/}
                                                {/*                                height={25}*/}
                                                {/*                                style={{cursor: 'pointer'}}*/}
                                                {/*                            />*/}
                                                {/*                        </Tooltip>*/}
                                                {/*                    </Typography>*/}
                                                {/*                    <Typography variant="caption">*/}
                                                {/*                        <Tooltip*/}
                                                {/*                            arrow*/}
                                                {/*                            placement="top"*/}
                                                {/*                            title={*/}
                                                {/*                                <>*/}
                                                {/*                                    <Typography*/}
                                                {/*                                        variant="h5"*/}
                                                {/*                                        component="div"*/}
                                                {/*                                    >*/}
                                                {/*                                        <div*/}
                                                {/*                                            dangerouslySetInnerHTML={{*/}
                                                {/*                                                __html: tooltip5.title,*/}
                                                {/*                                            }}*/}
                                                {/*                                        />*/}
                                                {/*                                    </Typography>*/}
                                                {/*                                    <Typography*/}
                                                {/*                                        variant="body1"*/}
                                                {/*                                        component="div"*/}
                                                {/*                                    >*/}
                                                {/*                                        <div*/}
                                                {/*                                            dangerouslySetInnerHTML={{*/}
                                                {/*                                                __html: tooltip5.tooltip,*/}
                                                {/*                                            }}*/}
                                                {/*                                        />*/}
                                                {/*                                    </Typography>*/}
                                                {/*                                </>*/}
                                                {/*                            }*/}
                                                {/*                        >*/}
                                                {/*                            <img*/}
                                                {/*                                src={abilityIcon(*/}
                                                {/*                                    player.character.replace(/:3/g, ''),*/}
                                                {/*                                    5,*/}
                                                {/*                                )}*/}
                                                {/*                                alt="ability5"*/}
                                                {/*                                width={25}*/}
                                                {/*                                height={25}*/}
                                                {/*                                style={{*/}
                                                {/*                                    marginRight: '10px',*/}
                                                {/*                                    cursor: 'pointer',*/}
                                                {/*                                }}*/}
                                                {/*                            />*/}
                                                {/*                        </Tooltip>*/}
                                                {/*                    </Typography>{' '}*/}
                                                {/*                    {customPlayer.CharacterInfo.CharacterCards*/}
                                                {/*                        .PrepCard !== 0 && (*/}
                                                {/*                        <Typography variant="caption">*/}
                                                {/*                            <img*/}
                                                {/*                                src={catalystsIcon(*/}
                                                {/*                                    customPlayer.CharacterInfo*/}
                                                {/*                                        .CharacterCards.PrepCard,*/}
                                                {/*                                )}*/}
                                                {/*                                alt={customPlayer.CharacterInfo.CharacterCards.PrepCard.toString()}*/}
                                                {/*                                width={25}*/}
                                                {/*                                height={25}*/}
                                                {/*                            />*/}
                                                {/*                        </Typography>*/}
                                                {/*                    )}*/}
                                                {/*                    {customPlayer.CharacterInfo.CharacterCards*/}
                                                {/*                        .DashCard !== 0 && (*/}
                                                {/*                        <Typography variant="caption">*/}
                                                {/*                            <img*/}
                                                {/*                                src={catalystsIcon(*/}
                                                {/*                                    customPlayer.CharacterInfo*/}
                                                {/*                                        .CharacterCards.DashCard,*/}
                                                {/*                                )}*/}
                                                {/*                                alt={customPlayer.CharacterInfo.CharacterCards.DashCard.toString()}*/}
                                                {/*                                width={25}*/}
                                                {/*                                height={25}*/}
                                                {/*                            />*/}
                                                {/*                        </Typography>*/}
                                                {/*                    )}*/}
                                                {/*                    {customPlayer.CharacterInfo.CharacterCards*/}
                                                {/*                        .CombatCard !== 0 && (*/}
                                                {/*                        <Typography variant="caption">*/}
                                                {/*                            <img*/}
                                                {/*                                src={catalystsIcon(*/}
                                                {/*                                    customPlayer.CharacterInfo*/}
                                                {/*                                        .CharacterCards.CombatCard,*/}
                                                {/*                                )}*/}
                                                {/*                                alt={customPlayer.CharacterInfo.CharacterCards.CombatCard.toString()}*/}
                                                {/*                                width={25}*/}
                                                {/*                                height={25}*/}
                                                {/*                            />*/}
                                                {/*                        </Typography>*/}
                                                {/*                    )}*/}
                                                {/*                </div>*/}
                                                {/*            );*/}
                                                {/*        }*/}
                                                {/*        return null; // If already rendered or does not match the current player's handle, render nothing*/}
                                                {/*    })}*/}
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
