import React, {useState} from 'react';
import {login} from "../../lib/Evos";
import {Box, Button, Container, CssBaseline, TextField, Typography} from "@mui/material";
import {useSignIn} from "react-auth-kit";
import {useNavigate} from "react-router-dom";
import ErrorDialog from "../generic/ErrorDialog";
import {EvosError, processError} from "../../lib/Error";

function LoginPage() {
    const signIn = useSignIn();
    const navigate = useNavigate();
    const [error, setError] = useState<EvosError>();

    const handleSubmit = (event: React.FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        const data = new FormData(event.currentTarget);
        const username = data.get('username') as string;
        const password = data.get('password') as string;

        if (!username || !password) {
            return;
        }

        const abort = new AbortController();
        login(abort, username, password)
            .then(resp => {
                signIn({
                    token: resp.data.token,
                    expiresIn: 24 * 60,
                    tokenType: "bearer",
                    authState: resp.data
                });
                navigate('/');
            })
            .catch((error) => processError(error, setError, () => setError({text: "Invalid username or password."})))

        return () => abort.abort();
    };

    return (
        <Container component="main" maxWidth="xs">
            {error && <ErrorDialog error={error} onDismiss={() => setError(undefined)} />}
            <CssBaseline />
            <Box
                sx={{
                    marginTop: 4,
                    display: 'flex',
                    flexDirection: 'column',
                    alignItems: 'center',
                }}
            >
                <Typography component="h1" variant="h5">
                    Log in
                </Typography>
                <Box component="form" onSubmit={handleSubmit} noValidate sx={{ mt: 1 }}>
                    <TextField
                        margin="normal"
                        required
                        fullWidth
                        id="username"
                        label="Username"
                        name="username"
                        autoComplete="username"
                        autoFocus
                    />
                    <TextField
                        margin="normal"
                        required
                        fullWidth
                        name="password"
                        label="Password"
                        type="password"
                        id="password"
                        autoComplete="current-password"
                    />
                    <Button
                        type="submit"
                        fullWidth
                        variant="contained"
                        sx={{ mt: 3, mb: 2 }}
                    >
                        Log In
                    </Button>
                </Box>
            </Box>
        </Container>
    );
}

export default LoginPage;
