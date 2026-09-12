import ConfirmDialog from '../../../components/ConfirmDialog/ConfirmDialog'
import CreateUserDialog from '../../../components/CreateUserDialog'
import EditUserDialog from '../../../components/EditUserDialog'
import PasswordRevealDialog from '../../../components/PasswordRevealDialog/PasswordRevealDialog'
import { useAdminAuth } from '../../../context/AdminAuthContext'
import AdministratorsTable from './AdministratorsTable'
import { useAdministratorsData } from './useAdministratorsData'
import { useAdministratorActions } from './useAdministratorActions'

import '../AdminVotingOptions/AdminVotingOptions.css'
import '../AdminUsers/AdminUsers.css'

function AdminAdministrators() {
    const { email: currentAdminEmail } = useAdminAuth()
    const data = useAdministratorsData()
    const actions = useAdministratorActions(data.fetchAdmins, data.page)

    const displayedError = data.error ?? actions.deleteError

    return (
        <div className="voting-options-page">
            <h1>Administrators</h1>

            <button className="voting-options-add-row-button" onClick={actions.openCreate}>
                Add administrator
            </button>

            {displayedError && <p className="voting-options-error">{displayedError}</p>}

            {data.isLoading ? (
                <p>Loading...</p>
            ) : (
                <AdministratorsTable
                    admins={data.admins}
                    currentAdminEmail={currentAdminEmail}
                    page={data.page}
                    totalPages={data.totalPages}
                    onEdit={actions.startEdit}
                    onDelete={actions.startDelete}
                    onPageChange={data.fetchAdmins}
                />
            )}

            {actions.showCreate && (
                <CreateUserDialog
                    title="New administrator"
                    submitLabel="Create administrator"
                    onCreate={actions.create}
                    onCancel={actions.cancelCreate}
                    error={actions.createError}
                />
            )}

            {actions.editingAdmin && (
                <EditUserDialog
                    title="Edit administrator"
                    initialEmail={actions.editingAdmin.email}
                    onSave={actions.saveEdit}
                    onCancel={actions.cancelEdit}
                    error={actions.editError}
                />
            )}

            {actions.deletingAdmin && (
                <ConfirmDialog
                    title="Remove administrator"
                    message={`Are you sure you want to remove "${actions.deletingAdmin.email}"?`}
                    confirmLabel="Remove"
                    onConfirm={actions.confirmDelete}
                    onCancel={actions.cancelDelete}
                />
            )}

            {actions.revealPassword && (
                <PasswordRevealDialog
                    email={actions.revealPassword.email}
                    password={actions.revealPassword.password}
                    onClose={actions.closeReveal}
                />
            )}
        </div>
    )
}

export default AdminAdministrators