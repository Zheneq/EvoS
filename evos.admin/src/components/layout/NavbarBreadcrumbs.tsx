import * as React from 'react';
import { useLocation, Link as RouterLink } from 'react-router-dom';
import { styled } from '@mui/material/styles';
import Typography from '@mui/material/Typography';
import Breadcrumbs, { breadcrumbsClasses } from '@mui/material/Breadcrumbs';
import Link from '@mui/material/Link';
import NavigateNextRoundedIcon from '@mui/icons-material/NavigateNextRounded';

const StyledBreadcrumbs = styled(Breadcrumbs)(({ theme }) => ({
  margin: theme.spacing(0.5, 0),
  [`& .${breadcrumbsClasses.separator}`]: {
    color: (theme.vars || theme).palette.action.disabled,
    margin: 1,
  },
  [`& .${breadcrumbsClasses.ol}`]: {
    alignItems: 'center',
  },
}));

const routeNameMap: Record<string, string> = {
  '': 'Status',
  'admin': 'Admin Panel',
  'codes': 'Codes',
  'game-servers': 'Game Servers',
  'account': 'Account',
  'chat': 'Chat History',
  'matches': 'Match History',
  'feedback': 'Feedback & Reports',
  'debug': 'Debug',
  'login': 'Login',
};

export default function NavbarBreadcrumbs() {
  const location = useLocation();
  const pathnames = location.pathname.split('/').filter((x) => x);

  return (
    <StyledBreadcrumbs
      aria-label="breadcrumb"
      separator={<NavigateNextRoundedIcon fontSize="small" />}
    >
      <Link
        component={RouterLink}
        to="/"
        underline="hover"
        color="inherit"
        sx={{ display: 'flex', alignItems: 'center' }}
      >
        <Typography variant="body2" sx={{ color: 'text.secondary', fontWeight: 500 }}>
          Dashboard
        </Typography>
      </Link>

      {pathnames.length === 0 && (
        <Typography variant="body2" sx={{ color: 'text.primary', fontWeight: 600 }}>
          Status
        </Typography>
      )}

      {pathnames.map((segment, index) => {
        const isLast = index === pathnames.length - 1;
        const to = `/${pathnames.slice(0, index + 1).join('/')}`;
        const name = routeNameMap[segment] || segment;

        return isLast ? (
          <Typography
            key={to}
            variant="body2"
            sx={{ color: 'text.primary', fontWeight: 600 }}
          >
            {name}
          </Typography>
        ) : (
          <Link
            key={to}
            component={RouterLink}
            to={to}
            underline="hover"
            color="inherit"
          >
            <Typography variant="body2" sx={{ color: 'text.secondary' }}>
              {name}
            </Typography>
          </Link>
        );
      })}
    </StyledBreadcrumbs>
  );
}
