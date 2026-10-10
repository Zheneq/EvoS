import * as React from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuthUser, useIsAuthenticated } from 'react-auth-kit';
import { styled } from '@mui/material/styles';
import Avatar from '@mui/material/Avatar';
import MuiDrawer, { drawerClasses } from '@mui/material/Drawer';
import Box from '@mui/material/Box';
import Divider from '@mui/material/Divider';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import LoginRoundedIcon from '@mui/icons-material/LoginRounded';
import MenuContent from './MenuContent';
import OptionsMenu from './OptionsMenu';
import { BannerType, logoSmall, playerBanner } from '../../lib/Resources';

const drawerWidth = 250;

const Drawer = styled(MuiDrawer)({
  width: drawerWidth,
  flexShrink: 0,
  boxSizing: 'border-box',
  [`& .${drawerClasses.paper}`]: {
    width: drawerWidth,
    boxSizing: 'border-box',
  },
});

export default function SideMenu() {
  const isAuthenticated = useIsAuthenticated();
  const auth = useAuthUser();
  const navigate = useNavigate();

  return (
    <Drawer
      variant="permanent"
      sx={{
        display: { xs: 'none', md: 'block' },
        [`& .${drawerClasses.paper}`]: {
          backgroundColor: 'background.paper',
          borderRight: '1px solid',
          borderColor: 'divider',
        },
      }}
    >
      {/* Brand Header */}
      <Box
        onClick={() => navigate('/')}
        sx={{
          display: 'flex',
          alignItems: 'center',
          gap: 1.5,
          p: 2,
          cursor: 'pointer',
          '&:hover': {
            opacity: 0.9,
          },
        }}
      >
        <Avatar
          alt="Atlas Reactor"
          variant="rounded"
          src={logoSmall()}
          sx={{
            width: 38,
            height: 38,
            border: '1px solid',
            borderColor: 'divider',
            backgroundColor: 'background.default',
          }}
        />
        <Box sx={{ overflow: 'hidden' }}>
          <Typography
            variant="subtitle1"
            sx={{
              fontWeight: 700,
              lineHeight: 1.2,
              letterSpacing: '-0.02em',
              whiteSpace: 'nowrap',
              overflow: 'hidden',
              textOverflow: 'ellipsis',
            }}
          >
            Atlas Reactor
          </Typography>
          <Typography
            variant="caption"
            sx={{
              color: 'text.secondary',
              fontWeight: 500,
              fontSize: '0.7rem',
              textTransform: 'uppercase',
              letterSpacing: '0.06em',
            }}
          >
            Admin Console
          </Typography>
        </Box>
      </Box>

      <Divider />

      {/* Navigation List */}
      <Box
        sx={{
          overflowY: 'auto',
          flexGrow: 1,
          display: 'flex',
          flexDirection: 'column',
        }}
      >
        <MenuContent />
      </Box>

      {/* User Footer */}
      <Divider />
      <Box sx={{ p: 1.5 }}>
        {isAuthenticated() ? (
          <Stack
            direction="row"
            sx={{
              gap: 1,
              alignItems: 'center',
              p: 0.5,
              borderRadius: 1.5,
            }}
          >
            <Avatar
              alt={auth()?.handle || 'User'}
              src={playerBanner(BannerType.foreground, auth()?.banner ?? 65)}
              sx={{
                width: 36,
                height: 36,
                border: '1px solid',
                borderColor: 'divider',
              }}
            />
            <Box sx={{ mr: 'auto', overflow: 'hidden' }}>
              <Typography
                variant="body2"
                sx={{
                  fontWeight: 600,
                  lineHeight: '16px',
                  whiteSpace: 'nowrap',
                  overflow: 'hidden',
                  textOverflow: 'ellipsis',
                }}
              >
                {auth()?.handle || 'Administrator'}
              </Typography>
              <Typography
                variant="caption"
                sx={{ color: 'text.secondary', fontSize: '0.72rem' }}
              >
                EvoS Manager
              </Typography>
            </Box>
            <OptionsMenu />
          </Stack>
        ) : (
          <Button
            variant="outlined"
            fullWidth
            size="small"
            startIcon={<LoginRoundedIcon />}
            onClick={() => navigate('/login')}
          >
            Log in
          </Button>
        )}
      </Box>
    </Drawer>
  );
}
