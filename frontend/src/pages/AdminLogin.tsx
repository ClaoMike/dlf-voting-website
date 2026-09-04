import { useState } from 'react'
import './AdminLogin.css'

const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

function AdminLogin() {
    const [email, setEmail] = useState('')
    const [password, setPassword] = useState('')

    const isEmailValid = EMAIL_REGEX.test(email)
    const isPasswordValid = password.length > 0
    const isFormValid = isEmailValid && isPasswordValid

    const handleSubmit = (e: React.FormEvent) => {
        e.preventDefault()
        if (!isFormValid) return
        // TODO: call login API
    }

    return (
        <div className="admin-login-page">
            <h1 className="admin-login-title">Administration</h1>
            <form className="admin-login-form" onSubmit={handleSubmit}>
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

                <button type="submit" disabled={!isFormValid}>
                    Login
                </button>
            </form>
        </div>
    )
}

export default AdminLogin