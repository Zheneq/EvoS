import React, {useEffect, useMemo, useState} from "react";
import {
    Accordion,
    AccordionDetails,
    AccordionSummary,
    Alert,
    Box,
    Button,
    Chip,
    FormControl,
    Grid,
    InputLabel,
    LinearProgress,
    MenuItem,
    Select,
    SelectChangeEvent,
    Snackbar,
    Stack,
    TextField,
    Typography
} from "@mui/material";
import {
    EvosServerMessageSeverity,
    EvosServerMessageType,
    getMotd,
    Language,
    makeServerMsgData,
    MessagesWithMetadata,
    setMotd,
    toMap
} from "../../lib/Evos";
import {useAuthHeader} from "react-auth-kit";
import {useNavigate} from "react-router-dom";
import {processError} from "../../lib/Error";
import SaveRoundedIcon from "@mui/icons-material/SaveRounded";
import RefreshRoundedIcon from "@mui/icons-material/RefreshRounded";
import TranslateRoundedIcon from "@mui/icons-material/TranslateRounded";
import ExpandMoreRoundedIcon from "@mui/icons-material/ExpandMoreRounded";
import CampaignRoundedIcon from "@mui/icons-material/CampaignRounded";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import CheckCircleOutlineRoundedIcon from "@mui/icons-material/CheckCircleOutlineRounded";
import WarningAmberRoundedIcon from "@mui/icons-material/WarningAmberRounded";
import ErrorOutlineRoundedIcon from "@mui/icons-material/ErrorOutlineRounded";

export const LANGUAGE_LABELS: Record<Language, string> = {
    [Language.en]: "English (en)",
    [Language.fr]: "French / Français (fr)",
    [Language.de]: "German / Deutsch (de)",
    [Language.ru]: "Russian / Русский (ru)",
    [Language.es]: "Spanish / Español (es)",
    [Language.it]: "Italian / Italiano (it)",
    [Language.pl]: "Polish / Polski (pl)",
    [Language.pt]: "Portuguese / Português (pt)",
    [Language.ko]: "Korean / 한국어 (ko)",
    [Language.zh]: "Chinese / 中文 (zh)",
    [Language.nl]: "Dutch / Nederlands (nl)",
    [Language.br]: "Portuguese Brazil (br)",
};

interface ServerMessageProps {
    type: EvosServerMessageType;
}

export default function ServerMessage({type}: ServerMessageProps) {
    const [msg, setMsg] = useState<string>();
    const [severityFeedback, setSeverityFeedback] = useState<"success" | "info" | "warning" | "error">("info");
    const [serverMessage, setServerMessage] = useState<Map<Language, string>>(new Map());
    const [serverMessageSeverity, setServerMessageSeverity] = useState<EvosServerMessageSeverity>(EvosServerMessageSeverity.Warning);
    const [loading, setLoading] = useState<boolean>(true);
    const [processing, setProcessing] = useState<boolean>(false);
    const authHeader = useAuthHeader();
    const navigate = useNavigate();

    const loadData = () => {
        setLoading(true);
        const abort = new AbortController();
        getMotd(abort, type)
            .then((resp) => {
                setServerMessage(
                    toMap(
                        Object.keys(Language),
                        lg => lg as Language,
                        lg => (resp.data.msg?.[lg] as string) || ""
                    )
                );
                setServerMessageSeverity(resp.data.severity || EvosServerMessageSeverity.Warning);
            })
            .catch(e => {
                setSeverityFeedback("error");
                processError(e, err => setMsg(err.text || "Failed to load announcement"), navigate);
            })
            .finally(() => setLoading(false));

        return () => abort.abort();
    };

    useEffect(() => {
        const cleanup = loadData();
        return cleanup;
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [type]);

    const handleChange = (lg: Language, value: string) => {
        setServerMessage(prev => {
            const next = new Map<Language, string>(prev);
            next.set(lg, value);
            return next;
        });
    };

    const handleUpdateSeverity = (event: SelectChangeEvent) => {
        setServerMessageSeverity(event.target.value as EvosServerMessageSeverity);
    };

    const handleSubmit = (event: React.FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        setProcessing(true);

        const abort = new AbortController();
        setMotd(abort, authHeader(), type, makeServerMsgData(serverMessage, serverMessageSeverity))
            .then(() => {
                setSeverityFeedback("success");
                setMsg("Announcement updated and saved successfully");
            })
            .catch(e => {
                setSeverityFeedback("error");
                processError(e, err => setMsg(err.text || "Failed to save announcement"), navigate);
            })
            .finally(() => setProcessing(false));

        return () => abort.abort();
    };

    const englishMessage = serverMessage.get(Language.en) || "";

    const otherLanguages = useMemo(() => {
        return (Object.keys(Language) as Language[]).filter(lg => lg !== Language.en);
    }, []);

    const activeTranslationsCount = useMemo(() => {
        let count = 0;
        for (const lg of otherLanguages) {
            if ((serverMessage.get(lg) || "").trim().length > 0) {
                count++;
            }
        }
        return count;
    }, [otherLanguages, serverMessage]);

    const isValid = !!englishMessage.trim() || activeTranslationsCount === 0;

    const severityIcon = (sev: EvosServerMessageSeverity) => {
        switch (sev) {
            case EvosServerMessageSeverity.Info:
                return <InfoOutlinedIcon fontSize="small" color="info" />;
            case EvosServerMessageSeverity.Success:
                return <CheckCircleOutlineRoundedIcon fontSize="small" color="success" />;
            case EvosServerMessageSeverity.Warning:
                return <WarningAmberRoundedIcon fontSize="small" color="warning" />;
            case EvosServerMessageSeverity.Error:
                return <ErrorOutlineRoundedIcon fontSize="small" color="error" />;
            default:
                return undefined;
        }
    };

    const severityColor = (sev: EvosServerMessageSeverity): "info" | "success" | "warning" | "error" | "default" => {
        switch (sev) {
            case EvosServerMessageSeverity.Info:
                return "info";
            case EvosServerMessageSeverity.Success:
                return "success";
            case EvosServerMessageSeverity.Warning:
                return "warning";
            case EvosServerMessageSeverity.Error:
                return "error";
            default:
                return "default";
        }
    };

    return (
        <Box component="form" onSubmit={handleSubmit} noValidate sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
            {loading && <LinearProgress sx={{ borderRadius: 1 }} />}

            {/* Severity Selector (when applicable) */}
            {MessagesWithMetadata.has(type) && (
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, flexWrap: 'wrap' }}>
                    <FormControl size="small" sx={{ minWidth: 220 }}>
                        <InputLabel id={`severity-label-${type}`}>Announcement Severity</InputLabel>
                        <Select
                            labelId={`severity-label-${type}`}
                            id={`severity-${type}`}
                            value={serverMessageSeverity as string}
                            label="Announcement Severity"
                            onChange={handleUpdateSeverity}
                            disabled={loading || processing}
                        >
                            {Object.keys(EvosServerMessageSeverity).map(s => (
                                <MenuItem key={s} value={s}>
                                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                                        {severityIcon(s as EvosServerMessageSeverity)}
                                        <span>{s}</span>
                                    </Box>
                                </MenuItem>
                            ))}
                        </Select>
                    </FormControl>

                    <Chip
                        label={`Banner: ${serverMessageSeverity}`}
                        color={severityColor(serverMessageSeverity)}
                        size="small"
                        icon={severityIcon(serverMessageSeverity)}
                        sx={{ fontWeight: 600 }}
                    />
                </Box>
            )}

            {/* Primary English Message */}
            <TextField
                required
                fullWidth
                multiline
                rows={3}
                id={`lang-${Language.en}`}
                label="Primary Message (English - Default & Fallback)"
                placeholder="Enter announcement text to display to players..."
                value={englishMessage}
                onChange={(e) => handleChange(Language.en, e.target.value)}
                disabled={loading || processing}
                helperText="Required. This message is served to all players whose client language does not have a translated version."
            />

            {/* Live Preview Panel */}
            <Box
                sx={{
                    p: 2,
                    borderRadius: 1.5,
                    border: '1px solid',
                    borderColor: 'divider',
                    backgroundColor: 'background.paper',
                    display: 'flex',
                    flexDirection: 'column',
                    gap: 1,
                }}
            >
                <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                        <CampaignRoundedIcon color="primary" fontSize="small" />
                        <Typography variant="caption" sx={{ fontWeight: 700, letterSpacing: '0.05em', textTransform: 'uppercase' }}>
                            Player View Preview
                        </Typography>
                    </Box>
                    {MessagesWithMetadata.has(type) && (
                        <Chip
                            label={serverMessageSeverity}
                            color={severityColor(serverMessageSeverity)}
                            size="small"
                            variant="filled"
                            sx={{ height: 20, fontSize: '0.7rem' }}
                        />
                    )}
                </Box>
                <Typography
                    variant="body2"
                    sx={{
                        fontStyle: englishMessage.trim() ? 'normal' : 'italic',
                        color: englishMessage.trim() ? 'text.primary' : 'text.secondary',
                        whiteSpace: 'pre-wrap',
                    }}
                >
                    {englishMessage.trim() || "No message currently set. English message will appear here."}
                </Typography>
            </Box>

            {/* Localized Translations Accordion */}
            <Accordion variant="outlined" sx={{ borderRadius: 1.5, overflow: 'hidden' }}>
                <AccordionSummary expandIcon={<ExpandMoreRoundedIcon />}>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, width: '100%', pr: 1 }}>
                        <TranslateRoundedIcon color="action" fontSize="small" />
                        <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                            Regional Translations
                        </Typography>
                        <Chip
                            label={`${activeTranslationsCount} / ${otherLanguages.length} configured`}
                            size="small"
                            color={activeTranslationsCount > 0 ? "primary" : "default"}
                            variant={activeTranslationsCount > 0 ? "filled" : "outlined"}
                            sx={{ ml: 'auto', fontWeight: 600 }}
                        />
                    </Box>
                </AccordionSummary>
                <AccordionDetails sx={{ pt: 1 }}>
                    <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 2 }}>
                        Leave any translation field blank to automatically fall back to the primary English message.
                    </Typography>

                    <Grid container spacing={2}>
                        {otherLanguages.map(lg => (
                            <Grid size={{ xs: 12, md: 6 }} key={lg}>
                                <TextField
                                    fullWidth
                                    multiline
                                    rows={2}
                                    size="small"
                                    id={`lang-${lg}`}
                                    label={LANGUAGE_LABELS[lg] || lg}
                                    placeholder={englishMessage || "Fallback to English"}
                                    value={serverMessage.get(lg) || ""}
                                    onChange={(e) => handleChange(lg, e.target.value)}
                                    disabled={loading || processing}
                                />
                            </Grid>
                        ))}
                    </Grid>
                </AccordionDetails>
            </Accordion>

            {/* Bottom Actions */}
            <Stack direction="row" spacing={1.5} sx={{ justifyContent: 'flex-end', pt: 1 }}>
                <Button
                    variant="outlined"
                    color="inherit"
                    disabled={loading || processing}
                    onClick={loadData}
                    startIcon={<RefreshRoundedIcon />}
                >
                    Discard Changes
                </Button>
                <Button
                    type="submit"
                    variant="contained"
                    color="primary"
                    disabled={loading || processing || !isValid}
                    startIcon={<SaveRoundedIcon />}
                >
                    Save Announcement
                </Button>
            </Stack>

            {processing && <LinearProgress sx={{ borderRadius: 1 }} />}

            <Snackbar
                open={!!msg}
                autoHideDuration={5000}
                onClose={() => setMsg(undefined)}
                anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
            >
                <Alert
                    onClose={() => setMsg(undefined)}
                    severity={severityFeedback}
                    variant="filled"
                    sx={{ width: '100%', boxShadow: 4 }}
                >
                    {msg}
                </Alert>
            </Snackbar>
        </Box>
    );
}