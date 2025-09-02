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