import * as React from 'react';
import Stack from '@mui/material/Stack';
import Box from '@mui/material/Box';
import Chip from '@mui/material/Chip';
import Tooltip from '@mui/material/Tooltip';
import SyncRoundedIcon from '@mui/icons-material/SyncRounded';
import NavbarBreadcrumbs from './NavbarBreadcrumbs';
import Search from './Search';
import ColorModeIconDropdown from '../../theme/ColorModeIconDropdown';
import { useLocalStorage } from '../../lib/useLocalStorage';
import { Settings, SettingsKey } from '../../lib/Settings';

export default function Header() {
  const [updateInBackground, setUpdateInBackground] = useLocalStorage(
    Settings.get(SettingsKey.updateInBackground)!
  );

  return (
    <Stack
      direction="row"
      sx={{
        display: { xs: 'none', md: 'flex' },
        width: '100%',
        alignItems: 'center',
        justifyContent: 'space-between',
        py: 1.5,
        px: 3,
        borderBottom: '1px solid',
        borderColor: 'divider',
        backgroundColor: 'background.paper',
        position: 'sticky',
        top: 0,
        zIndex: 10,
      }}
    >
      {/* Breadcrumbs */}
      <Box sx={{ minWidth: 0, flexShrink: 1 }}>
        <NavbarBreadcrumbs />
      </Box>

      {/* Header Actions */}
      <Stack direction="row" spacing={1.5} alignItems="center">
        <Search />

        <Tooltip
          title={`Background auto-refresh is ${updateInBackground ? 'ON' : 'OFF'}. Click to toggle.`}
        >
          <Chip
            size="small"
            icon={<SyncRoundedIcon sx={{ fontSize: '0.9rem !important' }} />}
            label={updateInBackground ? 'Auto-sync' : 'Manual sync'}
            color={updateInBackground ? 'primary' : 'default'}
            variant={updateInBackground ? 'filled' : 'outlined'}
            onClick={() => setUpdateInBackground(!updateInBackground)}
            sx={{
              cursor: 'pointer',
              fontWeight: 500,
              fontSize: '0.75rem',
            }}
          />
        </Tooltip>

        <ColorModeIconDropdown />
      </Stack>
    </Stack>
  );
}
