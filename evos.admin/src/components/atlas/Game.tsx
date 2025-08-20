import {GameData, GamePlayerData, PlayerData, Team} from "../../lib/Evos";
import {Box, Collapse, Slide, Stack, styled, Tooltip, Typography, useTheme} from "@mui/material";
import {FlexBox} from "../generic/BasicComponents";
import {mapMiniPic} from "../../lib/Resources";
import Player from "./Player";
import React, {useState} from "react";
import {CharacterIcon} from "./CharacterIcon";

export const TeamFlexBox = styled(FlexBox)(({ theme }) => ({
    paddingLeft: 20,
    paddingRight: 20,
    width: '40%',
    flexWrap: 'wrap',
}));


interface TeamProps {
    caption?: string;
    info: GamePlayerData[];
    isTeamA: boolean;
    playerData: Map<number, PlayerData>;
}

const TeamRow = React.forwardRef(({info, isTeamA, playerData}: TeamProps, ref) => {
    return (
        <TeamFlexBox ref={ref} flexGrow={1} flexShrink={1} flexBasis={'auto'}>
            {info.map((player, id) =>
                <CharacterIcon
                    key={`teamA_${id}`}
                    characterType={player.characterType}
                    data={playerData.get(player.accountId)}
                    team={isTeamA ? Team.TeamA : Team.TeamB}
                />)}
        </TeamFlexBox>
    )
});

function TeamList({caption, info, isTeamA, playerData}: TeamProps) {
    return (
        <Stack>
            {caption && <Typography variant={'h5'}>{caption}</Typography>}
            {info.map((p, id) =>
                <Stack key={`teamA_${id}`} direction={isTeamA ? 'row' : 'row-reverse'}>
                    <Player info={playerData.get(p.accountId)} />
                    <CharacterIcon
                        characterType={p.characterType}
                        data={playerData.get(p.accountId)}
                        team={isTeamA ? Team.TeamA : Team.TeamB}
                        rightSkew
                        noTooltip
                    />
                </Stack>
            )}
        </Stack>
    )
}

function statusString(info: GameData) {
    if (info.status === 'Started') {
        return `Turn ${info.turn}`;
    }
    if (info.turn === 0) {
        return info.status;
    }
    return `${info.status} (turn ${info.turn})`;
}

interface Props {
    info: GameData;
    playerData: Map<number, PlayerData>;
    expanded?: boolean;
}

export default function Game({info, playerData, expanded}: Props) {
    const A = {
        caption: "Team Blue",
        info: info.teamA,
        playerData: playerData,
        isTeamA: true,
    }
    const B = {
        caption: "Team Orange",
        info: info.teamB,
        playerData: playerData,
        isTeamA: false,
    }

    const theme = useTheme();

    const [collapsed, setCollapsed] = useState<boolean>(!expanded);

    return <>
        <Stack width={'100%'}>
            <FlexBox>
                <Slide in={collapsed} direction={"right"} mountOnEnter unmountOnExit><TeamRow {...A} /></Slide>
                <Tooltip title={`${info.map} ${info.ts}`} arrow>
                    <Box flexBasis={120} onClick={() => setCollapsed((x) => !x)} style={{
                        backgroundImage: `url(${mapMiniPic(info.map)})`,
                        backgroundSize: 'cover',
                        borderColor: 'white',
                        borderWidth: 2,
                        borderStyle: 'solid',
                        borderRadius: 8,
                        cursor: 'pointer',
                    }}>
                        <Typography variant={'h3'}>
                            <span style={{ textShadow: `2px 2px ${theme.palette.teamA.main}` }}>{info.teamAScore}</span>
                            <span> - </span>
                            <span style={{ textShadow: `2px 2px ${theme.palette.teamB.dark}` }}>{info.teamBScore}</span>
                        </Typography>
                        <Typography
                            variant={'caption'}
                            component={'div'}
                            style={{
                                textShadow: '1px 1px 2px black, -1px -1px 2px black, 1px -1px 2px black, -1px 1px 2px black',
                                marginTop: -10,
                            }}>
                            {statusString(info)}
                        </Typography>
                    </Box>
                </Tooltip>
                <Slide in={collapsed} direction={"left"} mountOnEnter unmountOnExit><TeamRow {...B} /></Slide>
            </FlexBox>
            <Collapse in={!collapsed}>
                <FlexBox style={{justifyContent : 'space-around', flexWrap: 'wrap'}} >
                    <TeamList {...A} />
                    <TeamList {...B} />
                </FlexBox>
            </Collapse>
        </Stack>
    </>;
}