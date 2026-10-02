import * as React from 'react';
import AppBar from '@mui/material/AppBar';
import Toolbar from '@mui/material/Toolbar';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Avatar from '@mui/material/Avatar';
import MenuRoundedIcon from '@mui/icons-material/MenuRounded';
import SideMenuMobile from './SideMenuMobile';
import MenuButton from './MenuButton';
import ColorModeIconDropdown from '../../theme/ColorModeIconDropdown';
import { logoSmall } from '../../lib/Resources';

export default function AppNavbar() {
  const [open, setOpen] = React.useState(false);

  const toggleDrawer = (newOpen: boolean) => () => {
    setOpen(newOpen);
  };

  return (
    <AppBar
      position="fixed"
      sx={{
        display: { xs: 'block', md: 'none' },
        boxShadow: 0,
        backgroundColor: 'background.paper',
        backgroundImage: 'none',
        borderBottom: '1px solid',
        borderColor: 'divider',
        zIndex: (theme) => theme.zIndex.appBar,
      }}
    >
      <Toolbar
        variant="dense"
        disableGutters
        sx={{
          px: 2,
          minHeight: 56,
          justifyContent: 'space-between',
        }}
      >
        <Stack direction="row" spacing={1.5} alignItems="center">
          <Avatar
            alt="Atlas Reactor"
            variant="rounded"
            src={logoSmall()}
            sx={{ width: 28, height: 28 }}
          />
          <Typography
            variant="h6"
            component="div"
            sx={{
              fontWeight: 700,
              fontSize: '1rem',
              color: 'text.primary',
            }}
          >
            Atlas Reactor
          </Typography>
        </Stack>

        <Stack direction="row" spacing={1} alignItems="center">
          <ColorModeIconDropdown />
          <MenuButton aria-label="Open menu" onClick={toggleDrawer(true)}>
            <MenuRoundedIcon fontSize="small" />
          </MenuButton>
        </Stack>

        <SideMenuMobile open={open} toggleDrawer={toggleDrawer} />
      </Toolbar>
    </AppBar>
  );
}
