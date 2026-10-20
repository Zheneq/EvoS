import { Theme, alpha, Components } from '@mui/material/styles';
import { svgIconClasses } from '@mui/material/SvgIcon';
import { typographyClasses } from '@mui/material/Typography';
import { buttonBaseClasses } from '@mui/material/ButtonBase';
import { chipClasses } from '@mui/material/Chip';
import { gray, red, green } from '../themePrimitives';

export const dataDisplayCustomizations: Components<Theme> = {
  MuiList: {
    styleOverrides: {
      root: {
        padding: '6px',
        display: 'flex',
        flexDirection: 'column',
        gap: '2px',
      },
    },
  },
  MuiListItem: {
    styleOverrides: {
      root: ({ theme }) => ({
        [`& .${svgIconClasses.root}`]: {
          width: '1.25rem',
          height: '1.25rem',
          color: (theme.vars || theme).palette.text.secondary,
        },
        [`& .${typographyClasses.root}`]: {
          fontWeight: 500,
        },
        [`& .${buttonBaseClasses.root}`]: {
          display: 'flex',
          gap: 12,
          padding: '8px 12px',
          borderRadius: (theme.vars || theme).shape.borderRadius,
          '&.Mui-selected': {
            backgroundColor: alpha(theme.palette.primary.main, 0.12),
            color: (theme.vars || theme).palette.primary.main,
            [`& .${svgIconClasses.root}`]: {
              color: (theme.vars || theme).palette.primary.main,
            },
            '&:hover': {
              backgroundColor: alpha(theme.palette.primary.main, 0.18),
            },
          },
        },
      }),
    },
  },
  MuiListItemText: {
    styleOverrides: {
      primary: ({ theme }) => ({
        fontSize: theme.typography.body2.fontSize,
        fontWeight: 500,
      }),
      secondary: ({ theme }) => ({
        fontSize: theme.typography.caption.fontSize,
      }),
    },
  },
  MuiListSubheader: {
    styleOverrides: {
      root: ({ theme }) => ({
        backgroundColor: 'transparent',
        padding: '6px 12px',
        fontSize: '0.75rem',
        fontWeight: 600,
        textTransform: 'uppercase',
        letterSpacing: '0.05em',
        color: (theme.vars || theme).palette.text.secondary,
      }),
    },
  },
  MuiListItemIcon: {
    styleOverrides: {
      root: {
        minWidth: 0,
      },
    },
  },
  MuiChip: {
    defaultProps: {
      size: 'small',
    },
    styleOverrides: {
      root: ({ theme }) => ({
        border: '1px solid',
        borderRadius: '999px',
        [`& .${chipClasses.label}`]: {
          fontWeight: 600,
        },
        variants: [
          {
            props: {
              color: 'default',
            },
            style: {
              borderColor: gray[200],
              backgroundColor: gray[100],
              [`& .${chipClasses.label}`]: {
                color: gray[600],
              },
              ...theme.applyStyles('dark', {
                borderColor: gray[700],
                backgroundColor: gray[800],
                [`& .${chipClasses.label}`]: {
                  color: gray[300],
                },
              }),
            },
          },
          {
            props: {
              color: 'success',
            },
            style: {
              borderColor: green[300],
              backgroundColor: green[50],
              [`& .${chipClasses.label}`]: {
                color: green[700],
              },
              ...theme.applyStyles('dark', {
                borderColor: green[800],
                backgroundColor: alpha(green[900], 0.3),
                [`& .${chipClasses.label}`]: {
                  color: green[300],
                },
              }),
            },
          },
          {
            props: {
              color: 'error',
            },
            style: {
              borderColor: red[200],
              backgroundColor: red[50],
              [`& .${chipClasses.label}`]: {
                color: red[700],
              },
              ...theme.applyStyles('dark', {
                borderColor: red[800],
                backgroundColor: alpha(red[900], 0.3),
                [`& .${chipClasses.label}`]: {
                  color: red[300],
                },
              }),
            },
          },
        ],
      }),
    },
  },
};
