import * as React from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import ListItemButton from '@mui/material/ListItemButton';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import Stack from '@mui/material/Stack';
import ListSubheader from '@mui/material/ListSubheader';
import DashboardRoundedIcon from '@mui/icons-material/DashboardRounded';
import AdminPanelSettingsRoundedIcon from '@mui/icons-material/AdminPanelSettingsRounded';
import ConfirmationNumberRoundedIcon from '@mui/icons-material/ConfirmationNumberRounded';
import StorageRoundedIcon from '@mui/icons-material/StorageRounded';
import PersonSearchRoundedIcon from '@mui/icons-material/PersonSearchRounded';
import { useAuthHeader, useAuthUser } from 'react-auth-kit';

function getAccountIdFromToken(token: string | null | undefined): string | null {
  if (!token) return null;
  try {
    const parts = token.split('.');
    if (parts.length < 2) return null;
    const base64Url = parts[1];
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
    const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');
    let decodedJson: string;
    try {
      decodedJson = decodeURIComponent(
        atob(padded)
          .split('')
          .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
          .join('')
      );
    } catch {
      decodedJson = atob(padded);
    }
    const decoded = JSON.parse(decodedJson);
    return (
      decoded?.nameid ||
      decoded?.['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] ||
      decoded?.sub ||
      null
    );
  } catch {
    return null;
  }
}

interface MenuItemDef {
  text: string;
  url: string;
  icon: React.ReactNode;
  matchPrefix?: boolean;
}

const mainNavItems: MenuItemDef[] = [
  { text: 'Lobby Status', url: '/', icon: <DashboardRoundedIcon fontSize="small" /> },
  { text: 'Admin Panel', url: '/admin', icon: <AdminPanelSettingsRoundedIcon fontSize="small" /> },
  { text: 'Codes', url: '/codes', icon: <ConfirmationNumberRoundedIcon fontSize="small" /> },
  { text: 'Game Servers', url: '/game-servers', icon: <StorageRoundedIcon fontSize="small" /> },
  { text: 'Account Search', url: '/account', icon: <PersonSearchRoundedIcon fontSize="small" />, matchPrefix: true },
];

interface MenuContentProps {
  onItemClick?: () => void;
}

export default function MenuContent({ onItemClick }: MenuContentProps) {
  const location = useLocation();
  const navigate = useNavigate();
  const auth = useAuthUser();
  const authHeader = useAuthHeader();

  const authData = auth();
  const rawToken =
    (typeof authData === 'string' ? authData : authData?.token) ||
    authHeader()?.replace(/^Bearer\s+/i, '');
  const accountId = getAccountIdFromToken(rawToken);

  const isSelected = (item: MenuItemDef) => {
    if (item.url === '/') {
      return location.pathname === '/';
    }
    if (item.matchPrefix) {
      return location.pathname.startsWith(item.url);
    }
    return location.pathname === item.url;
  };

  const handleNavigate = (url: string) => {
    navigate(url);
    if (onItemClick) {
      onItemClick();
    }
  };

  return (
    <Stack sx={{ flexGrow: 1, p: 1, justifyContent: 'space-between' }}>
      <List dense sx={{ gap: 0.5 }}>
        <ListSubheader sx={{ px: 1, py: 0.5, fontSize: '0.7rem' }}>
          Management
        </ListSubheader>
        {mainNavItems.map((item) => {
          const selected = isSelected(item);

          const targetUrl =
            item.url === '/account' && accountId ? `${item.url}/${accountId}` : item.url;

          return (
            <ListItem key={item.text} disablePadding sx={{ display: 'block' }}>
              <ListItemButton
                selected={selected}
                onClick={() => handleNavigate(targetUrl)}
                sx={{
                  borderRadius: 1.5,
                  py: 0.8,
                  px: 1.5,
                }}
              >
                <ListItemIcon sx={{ minWidth: 32 }}>{item.icon}</ListItemIcon>
                <ListItemText
                  primary={item.text}
                  primaryTypographyProps={{
                    fontSize: '0.875rem',
                    fontWeight: selected ? 600 : 500,
                  }}
                />
              </ListItemButton>
            </ListItem>
          );
        })}
      </List>
    </Stack>
  );
}
