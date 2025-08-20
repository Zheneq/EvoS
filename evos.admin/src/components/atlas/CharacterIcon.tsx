import {CharacterType, PlayerData, Team} from "../../lib/Evos";
import {ButtonBase, Tooltip, useTheme} from "@mui/material";
import {BgImage} from "../generic/BasicComponents";
import {characterIcon} from "../../lib/Resources";
import React from "react";
import {useNavigate} from "react-router-dom";


interface CharacterIconProps {
    characterType: CharacterType;
    data?: PlayerData;
    team: Team,
    rightSkew?: boolean;
    noTooltip?: boolean;
    small?: boolean;
}

export function CharacterIcon({characterType, data, team, rightSkew, noTooltip, small}: CharacterIconProps) {
    const navigate = useNavigate();
    const theme = useTheme();

    let transformOuter, transformInner;
    if (team === Team.TeamA || rightSkew) {
        transformOuter = theme.transform.skewA;
        transformInner = theme.transform.skewB;
    } else {
        transformOuter = theme.transform.skewB;
        transformInner = theme.transform.skewA;
    }
    const borderColor = team === Team.TeamA
        ? theme.palette.teamA.main
        : team === Team.TeamB
            ? theme.palette.teamB.main
            : team === Team.Spectator
                ? theme.palette.teamSpectator.main
                : theme.palette.teamOther.main;
    const handle = data?.handle ?? "UNKNOWN";

    const handleClick = () => {
        if (!data) {
            return;
        }
        navigate(`/account/${data.accountId}`);
    }

    const content = <ButtonBase
        focusRipple
        onClick={handleClick}
        style={{
            width: small ? 40 : 80,
            height: small ? 25 : 50,
            transform: transformOuter,
            overflow: 'hidden',
            borderColor: borderColor,
            borderWidth: 2,
            borderStyle: 'solid',
            borderRadius: 4,
            backgroundColor: '#333',
            margin: 2,
        }}
    >
        <div
            style={{
                transform: transformInner,
                width: '115%',
                height: '100%',
                flex: 'none',
            }}
        >
            <BgImage style={{
                backgroundImage: `url(${characterIcon(characterType)})`,
            }} />
        </div>
    </ButtonBase>;

    if (noTooltip) {
        return content;
    }

    return <Tooltip title={handle} arrow>{content}</Tooltip>;
}