import {Box, Button, FormControlLabel, Switch} from "@mui/material";
import dayjs from "dayjs";
import React from "react";
import {LocalizationProvider} from "@mui/x-date-pickers/LocalizationProvider";
import {AdapterDayjs} from "@mui/x-date-pickers/AdapterDayjs";
import {DateTimePicker} from "@mui/x-date-pickers/DateTimePicker";


interface HistoryNavButtonsProps<T> {
    items: T[];
    dateFunction: (item: T) => string;
    date: dayjs.Dayjs;
    setDate: (date: dayjs.Dayjs) => void;
    isBefore: boolean;
    setIsBefore: (isBefore: boolean) => void;
    disabled: boolean;
    datePicker: boolean;
    onChange?: () => void;
}

export default function HistoryNavButtons<T>(
    {items, dateFunction, date, setDate, isBefore, setIsBefore, disabled, datePicker, onChange}: HistoryNavButtonsProps<T>
) {
    const handleBackward = () => {
        if (items.length > 0) {
            const oldestMessageTime = dayjs(dateFunction(items[0])).subtract(1, 'ms');
            setDate(oldestMessageTime);
            setIsBefore(true);
        }
        onChange && onChange();
    };

    const handleForward = () => {
        if (items.length > 0) {
            const newestMessageTime = dayjs(dateFunction(items[items.length - 1])).add(1, 'ms');
            setDate(newestMessageTime);
            setIsBefore(false);
        }
        onChange && onChange();
    };

    const handleLatest = () => {
        const now = dayjs().add(1, 'minute');
        setDate(now);
        setIsBefore(true);
        onChange && onChange();
    };

    const handleDateChange = (newValue: dayjs.Dayjs | null) => {
        if (newValue) {
            setDate(newValue);
        }
        onChange && onChange();
    };

    const handleBeforeChange = (event: React.ChangeEvent<HTMLInputElement>) => {
        const newValue = event.target.checked;
        setIsBefore(newValue);
        onChange && onChange();
    };

    return <Box sx={{margin: '1em'}}>
        {datePicker &&
            <Box sx={{display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'center'}}>
                <FormControlLabel
                    control={
                        <Switch
                            checked={isBefore}
                            onChange={handleBeforeChange}
                            size="small"
                        />
                    }
                    label={isBefore ? "Showing items before" : "Showing items after"}
                />
                <LocalizationProvider dateAdapter={AdapterDayjs}>
                    <DateTimePicker
                        label="Date"
                        value={date}
                        onChange={handleDateChange}
                        slotProps={{textField: {size: 'small'}}}
                    />
                </LocalizationProvider>

            </Box>
        }
        <Box sx={{display: 'flex', justifyContent: 'center', gap: 2, my: 2}}>
            <Button
                variant="contained"
                onClick={handleBackward}
                disabled={disabled || items.length === 0}
            >
                ← Older
            </Button>
            <Button
                variant="contained"
                onClick={handleLatest}
                disabled={disabled}
            >
                Latest
            </Button>
            <Button
                variant="contained"
                onClick={handleForward}
                disabled={disabled || items.length === 0}
            >
                Newer →
            </Button>
        </Box>
    </Box>;
}