import { useState, type SubmitEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import './LoginForm.css'

const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

type LoginResult = { success: boolean; error?: string }

type LoginFormProps = {
    title: string
    login: (email: string, password: string) => Promise<LoginResult>
    redirectTo: string
}

function LoginForm({ title, login, redirectTo }: LoginFormProps) {
    const [email, setEmail] = useState('')
    const [password, setPassword] = useState('')
    const [serverError, setServerError] = useState<string | null>(null)
    const navigate = useNavigate()

    const isEmailValid = EMAIL_REGEX.test(email)
    const isPasswordValid = password.length > 0
    const isFormValid = isEmailValid && isPasswordValid

    const handleSubmit = async (e: SubmitEvent<HTMLFormElement>) => {
        e.preventDefault()
        if (!isFormValid) return

        setServerError(null)
        const result = await login(email, password)

        if (!result.success) {
            setServerError(result.error ?? 'Invalid email or password.')
            return
        }

        navigate(redirectTo)
    }

    return (
        <div className="login-page">
            <h1 className="login-title">{title}</h1>
            <form className="login-form" onSubmit={handleSubmit}>
                <label htmlFor="email">Email</label>
                <input
                    id="email"
                    type="email"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    placeholder="you@example.com"
                />
                {email.length > 0 && !isEmailValid && (
                    <span className="field-error">Enter a valid email address</span>
                )}

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