import { useState } from 'react'
import PasswordField from './PasswordField'
import { isValidEmail, isValidPassword } from '../utils/validation'
import './ConfirmDialog.css'

type EditUserDialogProps = {
    initialEmail: string
    onSave: (email: string | null, password: string | null) => void
    onCancel: () => void
    error: string | null
}

function EditUserDialog({ initialEmail, onSave, onCancel, error }: EditUserDialogProps) {
    const [email, setEmail] = useState(initialEmail)
    const [password, setPassword] = useState('')

    const trimmedEmail = email.trim()
    const emailChanged = trimmedEmail !== initialEmail.trim()
    const emailValid = isValidEmail(trimmedEmail)

    const passwordEntered = password.length > 0
    const passwordValid = !passwordEntered || isValidPassword(password)

    const canSubmit =
        (emailChanged || passwordEntered) &&
        emailValid &&
        passwordValid

    const handleSave = () => {
        onSave(emailChanged ? trimmedEmail : null, passwordEntered ? password : null)
    }

    return (
        <div className="confirm-dialog-overlay">
            <div className="confirm-dialog" role="dialog" aria-modal="true">
                <h2 className="confirm-dialog-title">Edit user</h2>

                <input
                    className="confirm-dialog-input"
                    type="email"
                    placeholder="Email"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                />
                {email.length > 0 && !emailValid && (
                    <p className="voting-options-error">Enter a valid email address.</p>
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