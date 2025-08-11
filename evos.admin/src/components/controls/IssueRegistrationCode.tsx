import {
    Box,
    Button,
    LinearProgress,
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableRow,
    TextField,
    Tooltip,
    Typography
} from "@mui/material";
import {formatDate, getRegistrationCodes, issueRegistrationCode, RegistrationCodeEntry} from "../../lib/Evos";
import React, {useEffect, useState} from "react";
import {useAuthHeader} from "react-auth-kit";
import {useNavigate} from "react-router-dom";
import {EvosError, processError} from "../../lib/Error";
import BaseDialog from "../generic/BaseDialog";
import {EvosCard, FlexBox, plainAccountLink} from "../generic/BasicComponents";

export default function IssueRegistrationCode() {
    const [code, setCode] = useState<string>();
    const [processing, setProcessing] = useState<boolean>();
    const [codes, setCodes] = useState<RegistrationCodeEntry[]>();
    const [codesBefore, setCodesBefore] = useState<Date>(new Date());
    const [error, setError] = useState<EvosError>();
    const authHeader = useAuthHeader()();
    const navigate = useNavigate();

    const handleSubmit = (event: React.FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        const data = new FormData(event.currentTarget);
        const issueFor = data.get('issueFor') as string;

        if (!issueFor) {
            return;
        }

        setProcessing(true);
        const abort = new AbortController();
        issueRegistrationCode(abort, authHeader, {issueFor: issueFor})
            .then((resp) => setCode(resp.data.code))
            .catch(e => processError(e, err => setCode(err.text), navigate))
            .then(() => {
                setProcessing(false);
                setCodesBefore(new Date(new Date().getTime() + 60000));
            });

        return () => abort.abort();
    };

    useEffect(() => {
        const abort = new AbortController();
        getRegistrationCodes(abort, authHeader, codesBefore)
            .then((resp) => {
                setCodes(resp.data.entries);
            })
            .catch((error) => processError(error, setError, navigate));

        return () => abort.abort();
    }, [authHeader, navigate, codesBefore]);

    return <FlexBox style={{ flexDirection: 'column' }}>
        <EvosCard variant="outlined">
            <Box component="form" onSubmit={handleSubmit} noValidate style={{ padding: 4 }}>
                <BaseDialog title={code} onDismiss={() => setCode(undefined)} copyTitle />
                <TextField
                    margin="normal"
                    required
                    fullWidth
                    id="issueFor"
                    label="Issue for"
                    name="issueFor"
                    autoFocus
                />
                <Button
                    disabled={processing}
                    type="submit"
                    fullWidth
                    variant="contained"
                    sx={{ mt: 3, mb: 2 }}
                >
                    Issue registration code
                </Button>
                {processing && <LinearProgress />}
            </Box>
        </EvosCard>

        {error && <Typography>{`Failed to load codes: ${error.text}${error.description ? `(${error.description})` : ""}`}</Typography>}
        {codes &&
            <Box style={{ margin: "0 auto" }}>
                <Table>
                    <TableHead>
                        <TableRow>
                            <TableCell>Issued to</TableCell>
                            <TableCell>Issued by</TableCell>
                            <TableCell>Code</TableCell>
                            <TableCell>Issued at</TableCell>
                            <TableCell>Expires at</TableCell>
                        </TableRow>
                    </TableHead>
                    <TableBody>
                        {codes.map((row) => {
                            const claimed= row.issuedTo !== 0;
                            const expired = !claimed && new Date(row.expiresAt) < new Date();
                            return <TableRow
                                key={row.code}
                                sx={{ '&:last-child td, &:last-child th': { border: 0 } }}
                            >
                                <TableCell>{claimed
                                    ? plainAccountLink(row.issuedTo, row.issuedToHandle, navigate)
                                    : row.issuedToHandle}</TableCell>
                                <TableCell>{plainAccountLink(row.issuedBy, row.issuedByHandle, navigate)}</TableCell>
                                <TableCell>{claimed || expired
                                    ? <Tooltip title={claimed ? "Claimed" : "Expired"}><span style={{ textDecoration: "line-through"}}>{row.code}</span></Tooltip>
                                    : <span>{row.code}</span>}
                                </TableCell>
                                <TableCell>{formatDate(row.issuedAt)}</TableCell>
                                <TableCell>{formatDate(row.expiresAt)}</TableCell>
                            </TableRow>
                        })}
                    </TableBody>
                </Table>
            </Box>
        }
    </FlexBox>;
}