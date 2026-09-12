import PasswordField from '../../../components/PasswordField/PasswordField'

type ChangePasswordSectionProps = {
    newPassword: string
    onPasswordChange: (value: string) => void
    isValid: boolean
    error: string | null
    onSubmit: () => void
}

function ChangePasswordSection({
                                   newPassword,
                                   onPasswordChange,
                                   isValid,
                                   error,
                                   onSubmit,
                               }: ChangePasswordSectionProps) {
    return (
        <section className="settings-password-section">
            <h2>Change your password</h2>

            <PasswordField value={newPassword} onChange={onPasswordChange} placeholder="New password" />
            {newPassword.length > 0 && !isValid && (
                <p className="voting-options-error">
                    Password must be 20-64 characters with at least one uppercase letter, one digit, and
                    one special character.
                </p>
            )}

            {error && <p className="voting-options-error">{error}</p>}

            <button
                className="settings-toggle-button settings-toggle-open"
                disabled={!isValid}
                onClick={onSubmit}
            >
                Update password
            </button>
        </section>
    )
}

export default ChangePasswordSection