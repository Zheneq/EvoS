import {PlayerData} from "../../lib/Evos";
import {ButtonBase, styled, Typography, useTheme} from "@mui/material";
import {BgImage} from "../generic/BasicComponents";
import {BannerType, playerBanner} from "../../lib/Resources";
import React from "react";
import {useNavigate} from "react-router-dom";

interface Props {
    info?: PlayerData;
    greyOut?: boolean;
    bot?: boolean;
}

const ImageTextWrapper = styled('span')(({ theme }) => ({
    position: 'absolute',
    left: '28%',
    color: theme.palette.common.white,
    textAlign: 'left',
    fontStretch: 'condensed',
    width: '100%',
    textShadow: '1px 1px 2px black, -1px -1px 2px black, 1px -1px 2px black, -1px 1px 2px black',
}));

function Player({info, greyOut, bot}: Props) {
    let username = 'OFFLINE', discriminator;
    if (info) {
        [username, discriminator] = info.handle.split('#', 2)
    } else if (bot) {
        username = 'Bot'
        info = {
            accountId: 0,
            handle: 'Bot',
            bannerBg: 95,
            bannerFg: 65,
            status: '',
            titleId: 0,
            factionData: {
                factions: [],
                selectedRibbonID: -1
            },
            buildVersion: '',
        }
    }

    const navigate = useNavigate();
    const theme = useTheme();

    const handleClick = () => {
        if (!info || bot) {
            return;
        }
        navigate(`/account/${info.accountId}`);
    }

    return <div
        style={{
            width: 240,
            height: 52,
            fontSize: '8px',
            position: 'relative',
            display: 'inline-flex',
            opacity: greyOut ? 0.5 : 1,
        }}>
        <ButtonBase
            focusRipple
            key={info?.handle}
            onClick={handleClick}
            style={{
                width: 240,
                height: 52,
                fontSize: '8px',
                transform: theme.transform.skewA,
                overflow: 'hidden',
                border: '2px solid black'
            }}
        >
            <div
                style={{
                    transform: theme.transform.skewB,
                    width: '106%',
                    height: '100%',
                    flex: 'none',
                }}
            >
                <BgImage style={{
                    backgroundImage: info && `url(${playerBanner(BannerType.background, info.bannerBg)})`,
                }} />
            </div>
        </ButtonBase>
        <div
            style={{
                width: 238,
                height: 49,
                overflow: 'hidden',
                position: 'absolute',
                top: 2,
                pointerEvents: "none",
            }}>
            <BgImage style={{
                marginTop: '-3%',
                marginLeft: '-4%',
                backgroundImage: info && `url(${playerBanner(BannerType.foreground, info.bannerFg)})`,
                width: '36%',
                zIndex: 0,
            }} />
            <ImageTextWrapper
                style={{
                    fontSize: '2.5em',
                }}>
                <Typography component={'span'} style={{ fontSize: '1em' }}>{username}</Typography>
                {discriminator && <Typography component={'span'} style={{ fontSize: '0.8em' }}>#{discriminator}</Typography>}
            </ImageTextWrapper>
            {info && <ImageTextWrapper
                style={{
                    bottom: '8%',
                    fontSize: '1.7em',
                }}>
                <Typography component={'span'} style={{ fontSize: '1em' }}>{info.status === "" && !bot ? "Online" : info.status}</Typography>
            </ImageTextWrapper>}
            {info && info.buildVersion &&
                <ImageTextWrapper style={{bottom: '2%', width: '69%', textAlign: 'right'}}>
                    <Typography component={'span'} style={{ fontSize: '1.4em' }}>
                        {info.buildVersion.replace(/^STABLE-122-100_/, '')}
                    </Typography>
                </ImageTextWrapper>}
        </div>
    </div>;
}

export default Player;