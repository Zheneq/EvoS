import {useState} from "react";
import dayjs from "dayjs";


export function useDateParamState(searchParams: URLSearchParams) {
    return useState(() => {
        const tsParam = searchParams.get('ts');
        return tsParam ? dayjs(parseInt(tsParam) * 1000) : dayjs();
    })
}

export function useBeforeParamState(searchParams: URLSearchParams) {
    return useState(() => {
        const beforeParam = searchParams.get('before');
        return beforeParam === null ? true : beforeParam === 'true';
    })
}

// Resolves to false instead of throwing when the clipboard is unavailable (e.g. served over plain HTTP)
export function copyToClipboard(text: string): Promise<boolean> {
    if (!navigator.clipboard) {
        console.error('Failed to copy text: clipboard is not available');
        return Promise.resolve(false);
    }
    return navigator.clipboard
        .writeText(text)
        .then(() => true)
        .catch(err => {
            console.error('Failed to copy text:', err);
            return false;
        });
}