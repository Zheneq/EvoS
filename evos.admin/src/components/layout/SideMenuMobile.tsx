import * as React from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuthUser, useIsAuthenticated, useSignOut } from 'react-auth-kit';
import Avatar from '@mui/material/Avatar';
import Button from '@mui/material/Button';
import Divider from '@mui/material/Divider';
import Drawer, { drawerClasses } from '@mui/material/Drawer';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Box from '@mui/material/Box';
import LogoutRoundedIcon from '@mui/icons-material/LogoutRounded';
import LoginRoundedIcon from '@mui/icons-material/LoginRounded';
import MenuContent from './MenuContent';
import { BannerType, logoSmall, playerBanner } from '../../lib/Resources';

interface SideMenuMobileProps {
  open: boolean;
  toggleDrawer: (newOpen: boolean) => () => void;
}

export default function SideMenuMobile({ open, toggleDrawer }: SideMenuMobileProps) {
  const isAuthenticated = useIsAuthenticated();
  const auth = useAuthUser();
  const signOut = useSignOut();
  const navigate = useNavigate();

  const handleLogout = () => {
    toggleDrawer(false)();
    signOut();
    navigate('/login');
  };

  const handleLogin = () => {
    toggleDrawer(false)();
    navigate('/login');
  };

  return (
    <Drawer
      anchor="right"
      open={open}
      onClose={toggleDrawer(false)}
      sx={{
        zIndex: (theme) => theme.zIndex.drawer + 1,
        [`& .${drawerClasses.paper}`]: {
          backgroundImage: 'none',
          backgroundColor: 'background.paper',
          width: '280px',
        },
      }}
    >
      <Stack sx={{ height: '100%' }}>
        {/* Mobile Header */}
        <Box sx={{ p: 2 }}>
          {isAuthenticated() ? (
            <Stack direction="row" spacing={1.5} alignItems="center">
              <Avatar
                alt={auth()?.handle || 'User'}
                src={playerBanner(BannerType.foreground, auth()?.banner ?? 65)}
                sx={{ width: 36, height: 36 }}
              />
              <Box sx={{ overflow: 'hidden' }}>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  {auth()?.handle || 'Administrator'}
                </Typography>
                <Typography variant="caption" sx={{ color: 'text.secondary' }}>
                  EvoS Admin
                </Typography>
              </Box>
            </Stack>
          ) : (
            <Stack direction="row" spacing={1.5} alignItems="center">
              <Avatar
                alt="Atlas Reactor"
                variant="rounded"
                src={logoSmall()}
                sx={{ width: 32, height: 32 }}
              />
              <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                Atlas Reactor
              </Typography>
            </Stack>
          )}
        </Box>

        <Divider />

        {/* Menu Items */}
        <Box sx={{ flexGrow: 1, overflowY: 'auto' }}>
          <MenuContent onItemClick={toggleDrawer(false)} />
        </Box>

        <Divider />

        {/* Footer Actions */}
        <Box sx={{ p: 2 }}>
          {isAuthenticated() ? (
            <Button
              variant="outlined"
              color="error"
              fullWidth
              startIcon={<LogoutRoundedIcon />}
              onClick={handleLogout}
            >
              Log out
            </Button>
          ) : (
            <Button
              variant="contained"
              fullWidth
              startIcon={<LoginRoundedIcon />}
              onClick={handleLogin}
            >
              Log in
            </Button>
          )}
        </Box>
      </Stack>
    </Drawer>
  );
}
