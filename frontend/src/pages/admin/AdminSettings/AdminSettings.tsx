import PasswordRevealDialog from '../../../components/PasswordRevealDialog/PasswordRevealDialog'
import { useAdminAuth } from '../../../context/AdminAuthContext'
import { useVotingStatus } from './useVotingStatus'
import { useChangePassword } from './useChangePassword'
import VotingToggleSection from './VotingToggleSection'
import ChangePasswordSection from './ChangePasswordSection'

import './AdminSettings.css'

function AdminSettings() {
    const { email } = useAdminAuth()
    const votingStatus = useVotingStatus()
    const changePassword = useChangePassword()

    return (
        <div className="voting-options-page">
            <h1>Settings</h1>

            {votingStatus.error && <p className="voting-options-error">{votingStatus.error}</p>}

            {votingStatus.isLoading ? (
                <p>Loading...</p>
            ) : (
                <VotingToggleSection
                    isVotingOpen={votingStatus.isVotingOpen ?? true}
                    onToggle={votingStatus.toggle}
                />
            )}

            <ChangePasswordSection
                newPassword={changePassword.newPassword}
                onPasswordChange={changePassword.setNewPassword}
                isValid={changePassword.isValid}
                error={changePassword.error}
                onSubmit={changePassword.submit}
            />

            {changePassword.revealPassword && email && (
                <PasswordRevealDialog
                    email={email}
                    password={changePassword.revealPassword}
                    onClose={changePassword.closeReveal}
                />
            )}
        </div>
    )
}

export default AdminSettings