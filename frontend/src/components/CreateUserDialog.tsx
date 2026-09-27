import { useState } from 'react'
import PasswordField from './PasswordField/PasswordField'
import { isValidEmail, isValidPassword, isValidUsername, USERNAME_MIN_LENGTH } from '../utils/validation'

import './ConfirmDialog/ConfirmDialog.css'

type CreateUserDialogProps = {
    onCreate: (username: string, email: string | null, password: string) => void
    onCancel: () => void
    error: string | null
    usernameMaxLength: number
    // Admins must have an email; users are created without one.
    emailField: 'required' | 'hidden'
    title?: string
    submitLabel?: string
}

function CreateUserDialog({
                              onCreate,
                              onCancel,
                              error,
                              usernameMaxLength,
                              emailField,
                              title = 'New user',
                              submitLabel = 'Create user',
                          }: CreateUserDialogProps) {
    const [username, setUsername] = useState('')
    const [email, setEmail] = useState('')
    const [password, setPassword] = useState('')

    const trimmedEmail = email.trim()
    const usernameValid = isValidUsername(username, usernameMaxLength)
    const emailValid = emailField === 'hidden' || isValidEmail(trimmedEmail)
    const passwordValid = isValidPassword(password)
    const canSubmit = usernameValid && emailValid && passwordValid

    return (
        <div className="confirm-dialog-overlay">
            <div className="confirm-dialog" role="dialog" aria-modal="true">
                <h2 className="confirm-dialog-title">{title}</h2>

                <input
                    className="confirm-dialog-input"
                    type="text"
                    autoCapitalize="none"
                    placeholder="Username"
                    value={username}
                    onChange={(e) => setUsername(e.target.value)}
                />
                {username.length > 0 && !usernameValid && (
                    <p className="voting-options-error">
                        Username must be {USERNAME_MIN_LENGTH}-{usernameMaxLength} characters without spaces.
                    </p>
                )}

                {emailField === 'required' && (
                    <>
                        <input
                            className="confirm-dialog-input"
                            type="email"
                            placeholder="Email"
                            value={email}
                            onChange={(e) => setEmail(e.target.value)}
                        />
                        {trimmedEmail.length > 0 && !emailValid && (
                            <p className="voting-options-error">Enter a valid email address.</p>
                        )}
                    </>
                )}

                <PasswordField value={password} onChange={setPassword} />
                {password.length > 0 && !passwordValid && (
                    <p className="voting-options-error">
                        Password must be 20-64 characters with at least one uppercase letter, one digit,
                        and one special character.
                    </p>
                )}

                {error && <p className="voting-options-error">{error}</p>}

                <div className="confirm-dialog-actions">
                    <button className="confirm-dialog-cancel" onClick={onCancel}>
                        Cancel
                    </button>
                    <button
                        className="confirm-dialog-confirm confirm-dialog-save"
                        disabled={!canSubmit}
                        onClick={() => onCreate(username.trim(), emailField === 'hidden' ? null : trimmedEmail, password)}
                    >
                        {submitLabel}
                    </button>
                </div>
            </div>
        </div>
    )
}

export default CreateUserDialog
