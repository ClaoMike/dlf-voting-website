import { useState } from 'react'
import PasswordField from './PasswordField/PasswordField'
import { isValidEmail, isValidPassword, isValidUsername, USERNAME_MIN_LENGTH } from '../utils/validation'

import './ConfirmDialog/ConfirmDialog.css'

type EditUserDialogProps = {
    initialUsername: string
    initialEmail: string | null
    // Receives the final username and email (null = keep for admins, none for users); password is null when unchanged.
    onSave: (username: string, email: string | null, password: string | null) => void
    onCancel: () => void
    error: string | null
    usernameMaxLength: number
    // 'hidden': the email is not editable and initialEmail is passed back unchanged (users).
    // 'keep-if-blank': an empty field means "keep the current email" (admins; the list doesn't carry it).
    emailField: 'hidden' | 'keep-if-blank'
    title?: string
}

function EditUserDialog({
                            initialUsername,
                            initialEmail,
                            onSave,
                            onCancel,
                            error,
                            usernameMaxLength,
                            emailField,
                            title = 'Edit user',
                        }: EditUserDialogProps) {
    const [username, setUsername] = useState(initialUsername)
    const [email, setEmail] = useState('')
    const [password, setPassword] = useState('')

    const trimmedUsername = username.trim()
    const trimmedEmail = email.trim()

    const usernameValid = isValidUsername(trimmedUsername, usernameMaxLength)
    const emailValid = emailField === 'hidden' || trimmedEmail.length === 0 || isValidEmail(trimmedEmail)

    const passwordEntered = password.length > 0
    const passwordValid = !passwordEntered || isValidPassword(password)

    const emailChanged = emailField === 'keep-if-blank' && trimmedEmail.length > 0
    const changed = trimmedUsername !== initialUsername || emailChanged || passwordEntered
    const canSubmit = changed && usernameValid && emailValid && passwordValid

    const handleSave = () => {
        const finalEmail = emailField === 'hidden' ? initialEmail : trimmedEmail || null
        onSave(trimmedUsername, finalEmail, passwordEntered ? password : null)
    }

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
                {!usernameValid && (
                    <p className="voting-options-error">
                        Username must be {USERNAME_MIN_LENGTH}-{usernameMaxLength} characters without spaces.
                    </p>
                )}

                {emailField === 'keep-if-blank' && (
                    <>
                        <input
                            className="confirm-dialog-input"
                            type="email"
                            placeholder="New email (leave blank to keep current)"
                            value={email}
                            onChange={(e) => setEmail(e.target.value)}
                        />
                        {!emailValid && <p className="voting-options-error">Enter a valid email address.</p>}
                    </>
                )}

                <PasswordField
                    value={password}
                    onChange={setPassword}
                    placeholder="New password (leave blank to keep current)"
                />
                {passwordEntered && !passwordValid && (
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
                        onClick={handleSave}
                    >
                        Save
                    </button>
                </div>
            </div>
        </div>
    )
}

export default EditUserDialog
