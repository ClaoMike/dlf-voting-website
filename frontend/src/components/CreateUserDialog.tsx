import { useState } from 'react'
import PasswordField from './PasswordField'
import { isValidEmail, isValidPassword } from '../utils/validation'
import './ConfirmDialog.css'

type CreateUserDialogProps = {
    onCreate: (email: string, password: string) => void
    onCancel: () => void
    error: string | null
}

function CreateUserDialog({ onCreate, onCancel, error }: CreateUserDialogProps) {
    const [email, setEmail] = useState('')
    const [password, setPassword] = useState('')

    const emailValid = isValidEmail(email)
    const passwordValid = isValidPassword(password)
    const canSubmit = emailValid && passwordValid

    return (
        <div className="confirm-dialog-overlay">
            <div className="confirm-dialog" role="dialog" aria-modal="true">
                <h2 className="confirm-dialog-title">New user</h2>

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
                        onClick={() => onCreate(email.trim(), password)}
                    >
                        Create user
                    </button>
                </div>
            </div>
        </div>
    )
}

export default CreateUserDialog