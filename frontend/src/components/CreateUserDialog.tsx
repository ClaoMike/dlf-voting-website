import { useId, useState } from 'react'
import Modal from './Modal/Modal'
import PasswordField from './PasswordField/PasswordField'
import { isValidEmail, isValidUsername, USERNAME_MIN_LENGTH, type PasswordPolicy } from '../utils/validation'

import './ConfirmDialog/ConfirmDialog.css'

type CreateUserDialogProps = {
    onCreate: (username: string, email: string | null, password: string) => void
    onCancel: () => void
    error: string | null
    usernameMaxLength: number
    // Admins must have an email; users are created without one.
    emailField: 'required' | 'hidden'
    passwordPolicy: PasswordPolicy
    title?: string
    submitLabel?: string
}

function CreateUserDialog({
                              onCreate,
                              onCancel,
                              error,
                              usernameMaxLength,
                              emailField,
                              passwordPolicy,
                              title = 'New user',
                              submitLabel = 'Create user',
                          }: CreateUserDialogProps) {
    const [username, setUsername] = useState('')
    const [email, setEmail] = useState('')
    const [password, setPassword] = useState('')
    const titleId = useId()
    const usernameId = useId()
    const usernameErrorId = useId()
    const emailId = useId()
    const emailErrorId = useId()

    const trimmedEmail = email.trim()
    const usernameValid = isValidUsername(username, usernameMaxLength)
    const emailValid = emailField === 'hidden' || isValidEmail(trimmedEmail)
    const passwordValid = passwordPolicy.isValid(password)
    const canSubmit = usernameValid && emailValid && passwordValid

    const showUsernameError = username.length > 0 && !usernameValid
    const showEmailError = trimmedEmail.length > 0 && !emailValid

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
                    aria-invalid={showUsernameError || undefined}
                    aria-describedby={usernameErrorId}
                />
                <p id={usernameErrorId} className="dialog-error" aria-live="polite">
                    {showUsernameError &&
                        `Username must be ${USERNAME_MIN_LENGTH}-${usernameMaxLength} characters without spaces.`}
                </p>
            </div>

            {emailField === 'required' && (
                <div className="dialog-field">
                    <label className="dialog-label" htmlFor={emailId}>Email</label>
                    <input
                        id={emailId}
                        className="confirm-dialog-input"
                        type="email"
                        autoComplete="off"
                        value={email}
                        onChange={(e) => setEmail(e.target.value)}
                        aria-invalid={showEmailError || undefined}
                        aria-describedby={emailErrorId}
                    />
                    <p id={emailErrorId} className="dialog-error" aria-live="polite">
                        {showEmailError && 'Enter a valid email address.'}
                    </p>
                </div>
            )}

            <PasswordField
                value={password}
                onChange={setPassword}
                hint={passwordPolicy.hint}
                generatedLength={passwordPolicy.generatedLength}
            />
            {password.length > 0 && !passwordValid && (
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
                    onClick={() => onCreate(username.trim(), emailField === 'hidden' ? null : trimmedEmail, password)}
                >
                    {submitLabel}
                </button>
            </div>
        </Modal>
    )
}

export default CreateUserDialog
