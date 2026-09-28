import ConfirmDialog from '../../../components/ConfirmDialog/ConfirmDialog'
import CreateUserDialog from '../../../components/CreateUserDialog'
import EditUserDialog from '../../../components/EditUserDialog'
import PasswordRevealDialog from '../../../components/PasswordRevealDialog/PasswordRevealDialog'
import { useAdminAuth } from '../../../context/useAdminAuth'
import { ADMIN_USERNAME_MAX_LENGTH } from '../../../utils/validation'
import AdministratorsTable from './AdministratorsTable'
import { useAdministratorsData } from './useAdministratorsData'
import { useAdministratorActions } from './useAdministratorActions'

import '../AdminVotingOptions/AdminVotingOptions.css'
import '../AdminUsers/AdminUsers.css'

function AdminAdministrators() {
    const { username: currentAdminUsername } = useAdminAuth()
    const data = useAdministratorsData()
    const actions = useAdministratorActions(data.fetchAdmins, data.page)

    const displayedError = data.error ?? actions.deleteError

    return (
        <div className="voting-options-page">
            <h1>Administrators</h1>

            <section className="admin-card" aria-label="Administrators">
                <div className="admin-card-toolbar">
                    <button className="voting-options-add-row-button" onClick={actions.openCreate}>
                        Add administrator
                    </button>
                </div>

                {displayedError && <p className="voting-options-error" role="alert">{displayedError}</p>}

                {data.isLoading ? (
                    <p role="status">Loading…</p>
                ) : (
                    <AdministratorsTable
                        admins={data.admins}
                        currentAdminUsername={currentAdminUsername}
                        page={data.page}
                        totalPages={data.totalPages}
                        onEdit={actions.startEdit}
                        onDelete={actions.startDelete}
                        onPageChange={data.fetchAdmins}
                    />
                )}
            </section>

            {actions.showCreate && (
                <CreateUserDialog
                    title="New administrator"
                    submitLabel="Create administrator"
                    usernameMaxLength={ADMIN_USERNAME_MAX_LENGTH}
                    emailField="required"
                    onCreate={actions.create}
                    onCancel={actions.cancelCreate}
                    error={actions.createError}
                />
            )}

            {actions.editingAdmin && (
                <EditUserDialog
                    title="Edit administrator"
                    initialUsername={actions.editingAdmin.username}
                    initialEmail={null}
                    usernameMaxLength={ADMIN_USERNAME_MAX_LENGTH}
                    emailField="keep-if-blank"
                    onSave={actions.saveEdit}
                    onCancel={actions.cancelEdit}
                    error={actions.editError}
                />
            )}

            {actions.deletingAdmin && (
                <ConfirmDialog
                    title="Remove administrator"
                    message={`Are you sure you want to remove "${actions.deletingAdmin.username}"?`}
                    confirmLabel="Remove"
                    onConfirm={actions.confirmDelete}
                    onCancel={actions.cancelDelete}
                />
            )}

            {actions.revealPassword && (
                <PasswordRevealDialog
                    accountName={actions.revealPassword.accountName}
                    password={actions.revealPassword.password}
                    onClose={actions.closeReveal}
                />
            )}
        </div>
    )
}

export default AdminAdministrators