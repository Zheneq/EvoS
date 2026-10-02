import { Theme } from '@mui/material/styles';
import type { PickerComponents } from '@mui/x-date-pickers/themeAugmentation';
import { menuItemClasses } from '@mui/material/MenuItem';
import { pickersDayClasses, yearCalendarClasses } from '@mui/x-date-pickers';
import { brand } from '../themePrimitives';

export const datePickersCustomizations: PickerComponents<Theme> = {
  MuiPickerPopper: {
    styleOverrides: {
      paper: ({ theme }) => ({
        marginTop: 4,
        borderRadius: (theme.vars || theme).shape.borderRadius,
        border: `1px solid ${(theme.vars || theme).palette.divider}`,
        backgroundImage: 'none',
        backgroundColor: (theme.vars || theme).palette.background.paper,
        boxShadow: (theme.vars || theme).shadows[3],
        [`& .${menuItemClasses.root}`]: {
          borderRadius: 6,
          margin: '0 6px',
        },
      }),
    },
  },
  MuiMonthCalendar: {
    styleOverrides: {
      button: ({ theme }) => ({
        fontSize: theme.typography.body1.fontSize,
        color: (theme.vars || theme).palette.text.secondary,
        padding: theme.spacing(0.5),
        borderRadius: (theme.vars || theme).shape.borderRadius,
        '&:hover': {
          backgroundColor: (theme.vars || theme).palette.action.hover,
        },
        [`&.${yearCalendarClasses.selected}`]: {
          backgroundColor: brand[500],
          color: '#fff',
          fontWeight: 600,
        },
      }),
    },
  },
  MuiYearCalendar: {
    styleOverrides: {
      button: ({ theme }) => ({
        fontSize: theme.typography.body1.fontSize,
        color: (theme.vars || theme).palette.text.secondary,
        padding: theme.spacing(0.5),
        borderRadius: (theme.vars || theme).shape.borderRadius,
        '&:hover': {
          backgroundColor: (theme.vars || theme).palette.action.hover,
        },
        [`&.${yearCalendarClasses.selected}`]: {
          backgroundColor: brand[500],
          color: '#fff',
          fontWeight: 600,
        },
      }),
    },
  },
  MuiPickersDay: {
    styleOverrides: {
      root: ({ theme }) => ({
        fontSize: theme.typography.body2.fontSize,
        color: (theme.vars || theme).palette.text.primary,
        borderRadius: (theme.vars || theme).shape.borderRadius,
        '&:hover': {
          backgroundColor: (theme.vars || theme).palette.action.hover,
        },
        [`&.${pickersDayClasses.selected}`]: {
          backgroundColor: brand[500],
          color: '#fff',
          fontWeight: 600,
          '&:hover': {
            backgroundColor: brand[600],
          },
        },
      }),
    },
  },
};
