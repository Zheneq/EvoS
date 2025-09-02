import {Box, Button} from "@mui/material";
import dayjs from "dayjs";
import {useSearchParams} from "react-router-dom";
import React from "react";

export const PARAM_TS = 'ts'

interface HistoryNavButtonsProps<T> {
    items: T[];
    dateFunction: (item: T) => string;
    setDate: (date: dayjs.Dayjs) => void;
    setIsBefore: (isBefore: boolean) => void;
    disabled: boolean;
}

export default function HistoryNavButtons<T>({items, dateFunction, setDate, setIsBefore, disabled}: HistoryNavButtonsProps<T>) {
    const [searchParams, setSearchParams] = useSearchParams();

    const handleBackward = () => {
        if (items.length > 0) {
            const oldestMessageTime = dayjs(dateFunction(items[0])).subtract(1, 'ms');
            setDate(oldestMessageTime);
            setIsBefore(true);
            const newParams = new URLSearchParams(searchParams);
            newParams.set(PARAM_TS, Math.floor(oldestMessageTime.unix()).toString());
            setSearchParams(newParams);
        }
    };

    const handleForward = () => {
        if (items.length > 0) {
            const newestMessageTime = dayjs(dateFunction(items[items.length - 1])).add(1, 'ms');
            setDate(newestMessageTime);
            setIsBefore(false);
            const newParams = new URLSearchParams(searchParams);
            newParams.set(PARAM_TS, Math.floor(newestMessageTime.unix()).toString());
            setSearchParams(newParams);
        }
    };

    const handleLatest = () => {
        const now = dayjs().add(1, 'minute');
        setDate(now);
        setIsBefore(true);
        const newParams = new URLSearchParams(searchParams);
        newParams.set(PARAM_TS, Math.floor(now.unix()).toString());
        setSearchParams(newParams);
    };

    return <Box sx={{display: 'flex', justifyContent: 'center', gap: 2, my: 2}}>
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
    </Box>;
}