import { Theme, alpha, Components } from '@mui/material/styles';
import { gray, orange } from '../themePrimitives';

export const feedbackCustomizations: Components<Theme> = {
  MuiAlert: {
    styleOverrides: {
      root: ({ theme }) => ({
        borderRadius: 8,
        backgroundColor: orange[100],
        color: (theme.vars || theme).palette.text.primary,
        border: `1px solid ${alpha(orange[300], 0.5)}`,
        '& .MuiAlert-icon': {
          color: orange[600],
        },
        ...theme.applyStyles('dark', {
          backgroundColor: alpha(orange[900], 0.35),
          border: `1px solid ${alpha(orange[800], 0.5)}`,
          color: orange[100],
        }),
      }),
    },
  },
  MuiDialog: {
    styleOverrides: {
      root: ({ theme }) => ({
        '& .MuiDialog-paper': {
          borderRadius: '12px',
          border: `1px solid ${(theme.vars || theme).palette.divider}`,
          backgroundImage: 'none',
          boxShadow: (theme.vars || theme).shadows[4],
        },
      }),
    },
  },
  MuiLinearProgress: {
    styleOverrides: {
      root: ({ theme }) => ({
        height: 6,
        borderRadius: 6,
        backgroundColor: gray[200],
        ...theme.applyStyles('dark', {
          backgroundColor: gray[800],
        }),
      }),
    },
  },
};
