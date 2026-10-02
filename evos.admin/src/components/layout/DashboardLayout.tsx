import * as React from 'react';
import { useLocation } from 'react-router-dom';
import Box from '@mui/material/Box';
import SideMenu from './SideMenu';
import AppNavbar from './AppNavbar';
import Header from './Header';
import ColorModeIconDropdown from '../../theme/ColorModeIconDropdown';

interface DashboardLayoutProps {
  children: React.ReactNode;
}

export default function DashboardLayout({ children }: DashboardLayoutProps) {
  const location = useLocation();
  const isLoginPage = location.pathname === '/login';

  if (isLoginPage) {
    return (
      <Box
        sx={{
          minHeight: '100vh',
          display: 'flex',
          flexDirection: 'column',
          backgroundColor: 'background.default',
        }}
      >
        <Box
          sx={{
            display: 'flex',
            justifyContent: 'flex-end',
            p: 2,
          }}
        >
          <ColorModeIconDropdown />
        </Box>
        <Box
          sx={{
            flexGrow: 1,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            p: 2,
          }}
        >
          {children}
        </Box>
      </Box>
    );
  }

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh', backgroundColor: 'background.default' }}>
      {/* Desktop Navigation */}
      <SideMenu />

      {/* Mobile Navigation Bar */}
      <AppNavbar />

      {/* Main Content Area */}
      <Box
        component="main"
        sx={{
          flexGrow: 1,
          minWidth: 0,
          display: 'flex',
          flexDirection: 'column',
          minHeight: '100vh',
          backgroundColor: 'background.default',
          overflow: 'hidden',
        }}
      >
        {/* Desktop Sticky Header */}
        <Header />

        {/* Scrollable Page Body */}
        <Box
          sx={{
            flexGrow: 1,
            overflowY: 'auto',
            p: { xs: 2, sm: 2.5, md: 3 },
            mt: { xs: '56px', md: 0 },
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
          }}
        >
          <Box
            sx={{
              width: '100%',
              maxWidth: { xs: '100%', xl: '1600px' },
              display: 'flex',
              flexDirection: 'column',
              gap: 2,
            }}
          >
            {children}
          </Box>
        </Box>
      </Box>
    </Box>
  );
}
