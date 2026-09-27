import PasswordRevealDialog from '../../../components/PasswordRevealDialog/PasswordRevealDialog'
import { useAdminAuth } from '../../../context/AdminAuthContext'
import { useVotingStatus } from './useVotingStatus'
import { useChangePassword } from './useChangePassword'
import VotingToggleSection from './VotingToggleSection'
import ChangePasswordSection from './ChangePasswordSection'

import '../AdminVotingOptions/AdminVotingOptions.css'
import './AdminSettings.css'

function AdminSettings() {
    const { username } = useAdminAuth()
    const votingStatus = useVotingStatus()
    const changePassword = useChangePassword()

    return (
        <div className="voting-options-page">
            <h1>Settings</h1>

            <section className="admin-card" aria-labelledby="settings-voting">
                <h2 id="settings-voting">Voting</h2>
                {votingStatus.error && <p className="voting-options-error" role="alert">{votingStatus.error}</p>}

                {votingStatus.isLoading ? (
                    <p role="status">Loading…</p>
                ) : (
                    <VotingToggleSection
                        isVotingOpen={votingStatus.isVotingOpen ?? true}
                        onToggle={votingStatus.toggle}
                    />
                )}
            </section>

            <ChangePasswordSection
                newPassword={changePassword.newPassword}
                onPasswordChange={changePassword.setNewPassword}
                isValid={changePassword.isValid}
                error={changePassword.error}
                onSubmit={changePassword.submit}
            />

            {changePassword.revealPassword && username && (
                <PasswordRevealDialog
                    accountName={username}
                    password={changePassword.revealPassword}
                    onClose={changePassword.closeReveal}
                />
            )}
        </div>
    )
}

export default AdminSettings