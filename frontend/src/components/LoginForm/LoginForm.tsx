import { useState, type SubmitEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { VOTING_SYSTEM_WEBSITE_TITLE } from '../../constants/strings'
import './LoginForm.css'

type LoginResult = { success: boolean; error?: string }

type LoginFormProps = {
    title: string
    intro?: string
    // Shown above the form, e.g. after the session ended.
    notice?: string
    login: (username: string, password: string) => Promise<LoginResult>
    redirectTo: string
}

function LoginForm({ title, intro, notice, login, redirectTo }: LoginFormProps) {
    const [username, setUsername] = useState('')
    const [password, setPassword] = useState('')
    const [showPassword, setShowPassword] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [isSubmitting, setIsSubmitting] = useState(false)
    const navigate = useNavigate()

    // The button stays enabled (a disabled button can't be focused or explain itself); empty fields get a message instead.
    const handleSubmit = async (e: SubmitEvent<HTMLFormElement>) => {
        e.preventDefault()
        if (isSubmitting) return

        if (username.trim().length === 0 || password.length === 0) {
            setError('Enter your username and password.')
            return
        }

        setError(null)
        setIsSubmitting(true)
        try {
            const result = await login(username.trim(), password)
            if (!result.success) {
                setError(result.error ?? 'Invalid username or password.')
                return
            }
            navigate(redirectTo)
        } catch {
            setError('Could not reach the server. Please try again.')
        } finally {
            setIsSubmitting(false)
        }
    }

    return (
        <div className="login-page">
            <div className="login-card">
                <p className="login-eyebrow">{VOTING_SYSTEM_WEBSITE_TITLE}</p>
                <h1 className="login-title">{title}</h1>
                {intro && <p className="login-intro">{intro}</p>}
                {notice && <p className="login-notice" role="status">{notice}</p>}

                <form className="login-form" onSubmit={handleSubmit} noValidate>
                    <label htmlFor="username">Username</label>
                    <input
                        id="username"
                        type="text"
                        autoComplete="username"
                        autoCapitalize="none"
                        autoCorrect="off"
                        spellCheck={false}
                        value={username}
                        onChange={(e) => setUsername(e.target.value)}
                        aria-invalid={error ? true : undefined}
                        aria-describedby="login-error"
                    />

                    <label htmlFor="password">Password</label>
                    <div className="login-password">
                        <input
                            id="password"
                            type={showPassword ? 'text' : 'password'}
                            autoComplete="current-password"
                            autoCapitalize="none"
                            autoCorrect="off"
                            spellCheck={false}
                            value={password}
                            onChange={(e) => setPassword(e.target.value)}
                            aria-invalid={error ? true : undefined}
                            aria-describedby="login-error"
                        />
                        <button
                            type="button"
                            className="login-password-toggle"
                            onClick={() => setShowPassword((v) => !v)}
                            aria-controls="password"
                            aria-label={showPassword ? 'Hide password' : 'Show password'}
                        >
                            {showPassword ? 'Hide' : 'Show'}
                        </button>
                    </div>

                    <p id="login-error" className="field-error" aria-live="assertive">
                        {error}
                    </p>

                    <button type="submit" className="login-submit" aria-disabled={isSubmitting}>
                        {isSubmitting ? 'Signing in…' : 'Sign in'}
                    </button>
                </form>
            </div>
        </div>
    )
}

export default LoginForm
