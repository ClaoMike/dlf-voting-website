import ConfirmDialog from '../../../components/ConfirmDialog/ConfirmDialog'
import CreateUserDialog from '../../../components/CreateUserDialog'
import EditUserDialog from '../../../components/EditUserDialog'
import PasswordRevealDialog from '../../../components/PasswordRevealDialog/PasswordRevealDialog'
import { USER_USERNAME_MAX_LENGTH } from '../../../utils/validation'
import ImportUsersDialog from './ImportUsersDialog'
import UserDetailsDialog from './UserDetailsDialog'
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

    const displayedError = data.error ?? actions.loadError ?? actions.removeAllError ?? importState.error

    return (
        <div className="voting-options-page">
            <h1>Users</h1>

            <UsersActionsRow
                onAddUser={actions.openCreate}
                onImportClick={importState.openModeDialog}
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

            {importState.importSummary && importState.importSummary.warnings.length > 0 && (
                <ul className="import-warnings">
                    {importState.importSummary.warnings.map((w) => (
                        <li key={`${w.row}-${w.message}`}>
                            Row {w.row}: {w.message}
                        </li>
                    ))}
                </ul>
            )}

            {displayedError && <p className="voting-options-error">{displayedError}</p>}

            {data.isLoading ? (
                <p>Loading...</p>
            ) : (
                <UsersTable
                    users={data.users}
                    page={data.page}
                    totalPages={data.totalPages}
                    onSelect={actions.selectUser}
                    onEdit={actions.startEdit}
                    onDelete={actions.startDelete}
                    onPageChange={data.fetchUsers}
                />
            )}

            {actions.showCreate && (
                <CreateUserDialog
                    onCreate={actions.create}
                    onCancel={actions.cancelCreate}
                    error={actions.createError}
                    usernameMaxLength={USER_USERNAME_MAX_LENGTH}
                    emailField="hidden"
                />
            )}

            {actions.editingUser && (
                <EditUserDialog
                    initialUsername={actions.editingUser.username}
                    initialEmail={actions.editingUser.email}
                    usernameMaxLength={USER_USERNAME_MAX_LENGTH}
                    emailField="hidden"
                    onSave={actions.saveEdit}
                    onCancel={actions.cancelEdit}
                    error={actions.editError}
                />
            )}

            {actions.selectedUser && (
                <UserDetailsDialog user={actions.selectedUser} onClose={actions.closeDetails} />
            )}

            {importState.showModeDialog && (
                <ImportUsersDialog onChoose={importState.chooseMode} onCancel={importState.cancelModeDialog} />
            )}

            {actions.deletingUser && (
                <ConfirmDialog
                    title="Remove user"
                    message={`Are you sure you want to remove "${actions.deletingUser.username}"?`}
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
                    accountName={actions.revealPassword.accountName}
                    password={actions.revealPassword.password}
                    onClose={actions.closeReveal}
                />
            )}
        </div>
    )
}

export default AdminUsers