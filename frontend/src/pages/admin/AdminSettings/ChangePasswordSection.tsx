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
        <section className="admin-card settings-password-section" aria-labelledby="settings-password">
            <h2 id="settings-password">Change your password</h2>

            <PasswordField
                value={newPassword}
                onChange={onPasswordChange}
                label="New password"
                hint="20-64 characters, with at least one uppercase letter, one digit and one special character."
            />
            {newPassword.length > 0 && !isValid && (
                <p className="voting-options-error" role="alert">This password does not meet the requirements above.</p>
            )}

            {error && <p className="voting-options-error" role="alert">{error}</p>}

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