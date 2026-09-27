import { useState, type SubmitEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import './LoginForm.css'

type LoginResult = { success: boolean; error?: string }

type LoginFormProps = {
    title: string
    login: (username: string, password: string) => Promise<LoginResult>
    redirectTo: string
}

function LoginForm({ title, login, redirectTo }: LoginFormProps) {
    const [username, setUsername] = useState('')
    const [password, setPassword] = useState('')
    const [serverError, setServerError] = useState<string | null>(null)
    const navigate = useNavigate()

    const isFormValid = username.trim().length > 0 && password.length > 0

    const handleSubmit = async (e: SubmitEvent<HTMLFormElement>) => {
        e.preventDefault()
        if (!isFormValid) return

        setServerError(null)
        const result = await login(username.trim(), password)

        if (!result.success) {
            setServerError(result.error ?? 'Invalid username or password.')
            return
        }

        navigate(redirectTo)
    }

    return (
        <div className="login-page">
            <h1 className="login-title">{title}</h1>
            <form className="login-form" onSubmit={handleSubmit}>
                <label htmlFor="username">Username</label>
                <input
                    id="username"
                    type="text"
                    autoComplete="username"
                    autoCapitalize="none"
                    value={username}
                    onChange={(e) => setUsername(e.target.value)}
                    placeholder="Username"
                />

                <label htmlFor="password">Password</label>
                <input
                    id="password"
                    type="password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    placeholder="Password"
                />

                {serverError && <span className="field-error">{serverError}</span>}

                <button type="submit" disabled={!isFormValid}>
                    Login
                </button>
            </form>
        </div>
    )
}

export default LoginForm