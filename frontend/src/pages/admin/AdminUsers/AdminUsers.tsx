import ConfirmDialog from '../../../components/ConfirmDialog/ConfirmDialog'
import CreateUserDialog from '../../../components/CreateUserDialog'
import EditUserDialog from '../../../components/EditUserDialog'
import PasswordRevealDialog from '../../../components/PasswordRevealDialog/PasswordRevealDialog'
import UsersActionsRow from './UsersActionsRow'
import UsersTable from './UsersTable'
import { useUsersData } from './useUsersData'
import { useUserActions } from './useUserActions'
import { useUserImport } from './useUserImport'

import '../AdminVotingOptions/AdminVotingOptions.css'
import './AdminUsers.css'

function AdminUsers() {
    const data = useUsersData()
    const actions = useUserActions(data.fetchUsers, data.page)
    const importState = useUserImport(() => data.fetchUsers(1))

    const displayedError = data.error ?? actions.removeAllError ?? importState.error

    return (
        <div className="voting-options-page">
            <h1>Users</h1>

            <UsersActionsRow
                onAddUser={actions.openCreate}
                onImportClick={importState.triggerFilePicker}
                isImporting={importState.isImporting}
                fileInputRef={importState.fileInputRef}
                onFileSelected={importState.handleFileSelected}
                canRemoveAll={data.users.length > 0}
                onRemoveAllClick={actions.openRemoveAllConfirm}
            />

            {importState.importSummary && (
                <p className="administrators-self-label">
                    Imported {importState.importSummary.created} user
                    {importState.importSummary.created === 1 ? '' : 's'}
                    {importState.importSummary.skipped > 0
                        ? `, skipped ${importState.importSummary.skipped} (see downloaded file for details)`
                        : ''}
                    .
                </p>
            )}

            {displayedError && <p className="voting-options-error">{displayedError}</p>}

            {data.isLoading ? (
                <p>Loading...</p>
            ) : (
                <UsersTable
                    users={data.users}
                    page={data.page}
                    totalPages={data.totalPages}
                    onEdit={actions.startEdit}
                    onDelete={actions.startDelete}
                    onPageChange={data.fetchUsers}
                />
            )}

            {actions.showCreate && (
                <CreateUserDialog onCreate={actions.create} onCancel={actions.cancelCreate} error={actions.createError} />
            )}

            {actions.editingUser && (
                <EditUserDialog
                    initialEmail={actions.editingUser.email}
                    onSave={actions.saveEdit}
                    onCancel={actions.cancelEdit}
                    error={actions.editError}
                />
            )}

            {actions.deletingUser && (
                <ConfirmDialog
                    title="Remove user"
                    message={`Are you sure you want to remove "${actions.deletingUser.email}"?`}
                    confirmLabel="Remove"
                    onConfirm={actions.confirmDelete}
                    onCancel={actions.cancelDelete}
                />
            )}

            {actions.showRemoveAllConfirm && (
                <ConfirmDialog
                    title="Remove all users"
                    message="Are you sure you want to remove all users? This cannot be undone."
                    confirmLabel="Remove all"
                    onConfirm={actions.confirmRemoveAll}
                    onCancel={actions.cancelRemoveAllConfirm}
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

export default AdminUsers