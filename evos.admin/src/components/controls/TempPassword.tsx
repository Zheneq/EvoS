import {
    Alert,
    Box,
    Button,
    Card,
    IconButton,
    LinearProgress,
    Stack,
    Tooltip,
    Typography
} from "@mui/material";
import {generateTempPassword} from "../../lib/Evos";
import {useAuthHeader} from "react-auth-kit";
import BaseDialog from "../generic/BaseDialog";
import React, {useState} from "react";
import {processError} from "../../lib/Error";
import {useNavigate} from "react-router-dom";
import VpnKeyRoundedIcon from "@mui/icons-material/VpnKeyRounded";
import ContentCopyRoundedIcon from "@mui/icons-material/ContentCopyRounded";
import CheckRoundedIcon from "@mui/icons-material/CheckRounded";

interface Props {
    accountId: number;
}

export default function TempPassword({accountId}: Props) {
    const [msg, setMsg] = useState<string>();
    const [copied, setCopied] = useState(false);
    const [processing, setProcessing] = useState<boolean>();
    const authHeader = useAuthHeader();
    const navigate = useNavigate();

    const handleClick = () => {
        setProcessing(true);
        setCopied(false);
        const abort = new AbortController();
        generateTempPassword(abort, authHeader(), accountId)
            .then((resp) => setMsg(resp.data.code))
            .catch(e => processError(e, err => setMsg(err.text), navigate))
            .then(() => setProcessing(false));

        return () => abort.abort();
    };

    const handleCopy = () => {
        if (!msg) return;
        navigator.clipboard.writeText(msg).then(() => {
            setCopied(true);
            setTimeout(() => setCopied(false), 2000);
        });
    };

    return (
        <Card variant="outlined">
            <BaseDialog title={msg} onDismiss={() => setMsg(undefined)} copyTitle />

            <Stack spacing={2} sx={{ width: '100%' }}>
                <Box>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                        <VpnKeyRoundedIcon color="primary" />
                        <Typography variant="h6" sx={{ fontWeight: 600 }}>
                            Temporary Password
                        </Typography>
                    </Box>
                    <Typography variant="body2" color="text.secondary">
                        Generate a single-use temporary password for player account access or recovery.
                    </Typography>
                </Box>

                {msg && (
                    <Alert
                        severity="info"
                        icon={<VpnKeyRoundedIcon />}
                        sx={{
                            display: 'flex',
                            alignItems: 'center',
                            '& .MuiAlert-message': { width: '100%' }
                        }}
                    >
                        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', width: '100%' }}>
                            <Box>
                                <Typography variant="caption" sx={{ textTransform: 'uppercase', letterSpacing: 0.5, fontWeight: 600 }}>
                                    Generated Password:
                                </Typography>
                                <Typography
                                    variant="h6"
                                    component="div"
                                    sx={{
                                        fontFamily: 'monospace',
                                        fontWeight: 700,
                                        letterSpacing: 1.5,
                                    }}
                                >
                                    {msg}
                                </Typography>
                            </Box>
                            <Tooltip title={copied ? "Copied!" : "Copy password"}>
                                <IconButton size="small" onClick={handleCopy} color={copied ? "success" : "default"}>
                                    {copied ? <CheckRoundedIcon fontSize="small" /> : <ContentCopyRoundedIcon fontSize="small" />}
                                </IconButton>
                            </Tooltip>
                        </Box>
                    </Alert>
                )}

                <Button
                    variant="outlined"
                    color="primary"
                    startIcon={<VpnKeyRoundedIcon />}
                    disabled={processing || accountId === 0}
                    onClick={handleClick}
                    fullWidth
                >
                    Generate Temporary Password
                </Button>

                {processing && <LinearProgress sx={{ borderRadius: 1 }} />}
            </Stack>
        </Card>
    );
}