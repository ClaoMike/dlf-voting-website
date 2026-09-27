import { useId, useState } from 'react'
import Modal from './Modal/Modal'
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
    const titleId = useId()
    const usernameId = useId()
    const usernameErrorId = useId()
    const emailId = useId()
    const emailErrorId = useId()

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
        <Modal titleId={titleId} onClose={onCancel}>
            <h2 id={titleId} className="confirm-dialog-title">{title}</h2>

            <div className="dialog-field">
                <label className="dialog-label" htmlFor={usernameId}>Username</label>
                <input
                    id={usernameId}
                    className="confirm-dialog-input"
                    type="text"
                    autoCapitalize="none"
                    value={username}
                    onChange={(e) => setUsername(e.target.value)}
                    aria-invalid={!usernameValid || undefined}
                    aria-describedby={usernameErrorId}
                />
                <p id={usernameErrorId} className="dialog-error" aria-live="polite">
                    {!usernameValid &&
                        `Username must be ${USERNAME_MIN_LENGTH}-${usernameMaxLength} characters without spaces.`}
                </p>
            </div>

            {emailField === 'keep-if-blank' && (
                <div className="dialog-field">
                    <label className="dialog-label" htmlFor={emailId}>New email</label>
                    <p className="dialog-hint">Leave blank to keep the current email.</p>
                    <input
                        id={emailId}
                        className="confirm-dialog-input"
                        type="email"
                        autoComplete="off"
                        value={email}
                        onChange={(e) => setEmail(e.target.value)}
                        aria-invalid={!emailValid || undefined}
                        aria-describedby={emailErrorId}
                    />
                    <p id={emailErrorId} className="dialog-error" aria-live="polite">
                        {!emailValid && 'Enter a valid email address.'}
                    </p>
                </div>
            )}

            <PasswordField
                value={password}
                onChange={setPassword}
                label="New password"
                hint="Leave blank to keep the current password. 20-64 characters, with at least one uppercase letter, one digit and one special character."
            />
            {passwordEntered && !passwordValid && (
                <p className="dialog-error" role="alert">This password does not meet the requirements above.</p>
            )}

            {error && <p className="dialog-error" role="alert">{error}</p>}

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
        </Modal>
    )
}

export default EditUserDialog
