import * as React from 'react';
import {useState} from 'react';
import AppBar from '@mui/material/AppBar';
import Box from '@mui/material/Box';
import Toolbar from '@mui/material/Toolbar';
import Container from '@mui/material/Container';
import Avatar from '@mui/material/Avatar';
import {BannerType, logo, logoSmall, playerBanner} from "../../lib/Resources";
import {NavLink, useNavigate} from "react-router-dom";
import {useAuthUser, useIsAuthenticated, useSignOut} from "react-auth-kit";
import {
    FormControlLabel,
    IconButton,
    Menu,
    MenuItem,
    Stack,
    styled,
    Switch,
    TextField,
    Typography
} from "@mui/material";
import {EvosError} from "../../lib/Error";
import ErrorDialog from "./ErrorDialog";
import {useLocalStorage} from "../../lib/useLocalStorage";
import {Settings, SettingsKey} from "../../lib/Settings";

const pages = [
    { text: "Status", url: '/' },
    { text: "Admin panel", url: '/admin' },
    { text: "Codes", url: '/codes' },
    { text: "Game servers", url: '/game-servers' },
];

export const NavBarLink = styled(NavLink)({
    textDecoration: 'none',
});

export const NavBarText = styled(Typography)({
    color: 'white',
    display: 'block',
    textDecoration: 'none',
    fontFamily: '"Roboto","Helvetica","Arial",sans-serif',
    fontWeight: 500,
    fontSize: '0.875rem',
    lineHeight: 1.75,
    letterSpacing: '0.02857em',
    textTransform: 'uppercase',
    minWidth: 64,
    padding: '6px 8px',
});


export default function NavBar() {
    const [anchorUserMenu, setAnchorUserMenu] = useState<null | HTMLElement>(null);
    const [anchorNavMenu, setAnchorNavMenu] = useState<null | HTMLElement>(null);
    const [query, setQuery] = useState<string>("");
    const [error, setError] = useState<EvosError>();
    
    const [updateInBackground, setUpdateInBackground] = useLocalStorage(Settings.get(SettingsKey.updateInBackground)!!);

    const isAuthenticated = useIsAuthenticated();
    const signOut = useSignOut();
    const auth = useAuthUser();
    const navigate = useNavigate();

    const handleUserMenu = (event: React.MouseEvent<HTMLElement>) => {
        setAnchorUserMenu(event.currentTarget);
    };

    const handleUserMenuClose = () => {
        setAnchorUserMenu(null);
    };

    const handleNavMenu = (event: React.MouseEvent<HTMLElement>) => {
        setAnchorNavMenu(event.currentTarget);
    };

    const handleNavMenuClose = () => {
        setAnchorNavMenu(null);
    };

    const handleLogOut = () => {
        handleUserMenuClose();
        signOut();
        navigate('/login');
    };

    const handleSearchChange = (event: React.ChangeEvent<HTMLInputElement>) => {
        setQuery(event.target.value);
    }

    const handleSearchKeyDown = (event: React.KeyboardEvent<HTMLInputElement>) => {
        if (event.key !== "Enter" || !query) return;

        event.preventDefault();
        handleUserMenuClose();
        handleNavMenuClose();
        navigate(`/account/?query=${query}`);
    }

    return (
        <AppBar position="static">
            {error && <ErrorDialog error={error} onDismiss={() => setError(undefined)} />}
            <Container maxWidth="xl">
                <Toolbar disableGutters>
                    <Avatar alt="logo" variant="square" src={logo()} sx={{ flexShrink: 1, width: 255, height: 40, display: { xs: 'none', md: 'flex' } }}/>
                    <Box sx={{ flexGrow: 1, display: { xs: 'flex', md: 'none' } }}>
                        <IconButton
                            size="large"
                            aria-label="account of current user"
                            aria-controls="menu-appbar"
                            aria-haspopup="true"
                            onClick={handleNavMenu}
                            color="inherit"
                        >
                            <Avatar alt="logo" variant="square" src={logoSmall()} sx={{ flexShrink: 1, width: 40, height: 40 }}/>
                        </IconButton>
                        {isAuthenticated() && <Menu
                            id="menu-appbar"
                            anchorEl={anchorNavMenu}
                            anchorOrigin={{
                                vertical: 'bottom',
                                horizontal: 'left',
                            }}
                            keepMounted
                            transformOrigin={{
                                vertical: 'top',
                                horizontal: 'left',
                            }}
                            open={Boolean(anchorNavMenu)}
                            onClose={handleNavMenuClose}
                            sx={{
                                display: { xs: 'block', md: 'none' },
                            }}
                        >
                            {pages.map((page) => (
                                <MenuItem key={page.text} onClick={() => {
                                    handleNavMenuClose();
                                    navigate(page.url);
                                }}>
                                    <Typography textAlign="center">{page.text}</Typography>
                                </MenuItem>
                            ))}
                            <MenuItem key={'search-item'}>
                                <TextField
                                    id="account-search"
                                    type="search"
                                    label="Find player"
                                    variant="outlined"
                                    value={query}
                                    onChange={handleSearchChange}
                                    onKeyDown={handleSearchKeyDown}
                                />
                            </MenuItem>
                        </Menu>}
                    </Box>
                    <Stack direction={"row"} alignItems="center" sx={{ flexGrow: 1, display: { xs: 'none', md: 'flex' }, justifyContent: 'space-evenly' }}>
                        {isAuthenticated() && pages.map((page) => (
                            <NavBarLink key={page.text} to={page.url}><NavBarText>{page.text}</NavBarText></NavBarLink>
                        ))}
                        {isAuthenticated() &&
                            <TextField
                                id="account-search"
                                type="search"
                                label="Find player"
                                variant="outlined"
                                size="small"
                                value={query}
                                onChange={handleSearchChange}
                                onKeyDown={handleSearchKeyDown}
                            />}
                    </Stack>
                    <Box sx={{ flexGrow: 0 }}>
                        {isAuthenticated() && <>
                            <Stack direction={"row"} alignItems="center" sx={{cursor: "pointer"}} onClick={handleUserMenu}>
                                <NavBarText>{auth()?.handle}</NavBarText>
                                <Avatar
                                    alt="Avatar"
                                    src={playerBanner(BannerType.foreground, auth()?.banner ?? 65)}
                                    sx={{ width: 64, height: 64 }}
                                />
                            </Stack>
                            <Menu
                                id="menu-appbar"
                                anchorEl={anchorUserMenu}
                                anchorOrigin={{
                                    vertical: 'bottom',
                                    horizontal: 'right',
                                }}
                                keepMounted
                                transformOrigin={{
                                    vertical: 'top',
                                    horizontal: 'right',
                                }}
                                open={Boolean(anchorUserMenu)}
                                onClose={handleUserMenuClose}
                            >
                                <MenuItem onClick={handleLogOut}>Log out</MenuItem>
                                <MenuItem>
                                    <FormControlLabel
                                        control={
                                            <Switch
                                                checked={updateInBackground}
                                                onChange={(e) => setUpdateInBackground(e.target.checked)}
                                            />
                                        }
                                        label="Update in background"
                                    />
                                </MenuItem>
                            </Menu>
                        </>}
                        {!isAuthenticated() && <NavBarLink to='/login' style={(active) => active && { display: 'none' }}>
                            <NavBarText>Log in</NavBarText>
                        </NavBarLink>}
                    </Box>
                </Toolbar>
            </Container>
        </AppBar>
    );
}