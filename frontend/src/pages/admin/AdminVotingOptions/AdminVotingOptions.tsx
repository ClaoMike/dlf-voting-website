import ConfirmDialog from '../../../components/ConfirmDialog/ConfirmDialog'
import EditNameDialog from '../../../components/EditNameDialog'
import AddOptionRow from './AddOptionRow'
import VotingOptionsTable from './VotingOptionsTable'
import { useVotingOptionsData } from './useVotingOptionsData'
import { useVotingOptionActions } from './useVotingOptionActions'

import './AdminVotingOptions.css'

function AdminVotingOptions() {
    const data = useVotingOptionsData()
    const actions = useVotingOptionActions(data.fetchOptions)

    const displayedError = data.error ?? actions.error

    return (
        <div className="voting-options-page">
            <h1>Voting Options</h1>

            <AddOptionRow
                newName={actions.newName}
                onNameChange={actions.setNewName}
                isAdding={actions.isAdding}
                onAdd={actions.add}
            />

            <button
                className="voting-options-remove-all"
                disabled={data.options.length === 0}
                onClick={actions.openRemoveAllConfirm}
            >
                Remove all
            </button>

            {displayedError && <p className="voting-options-error">{displayedError}</p>}

            {data.isLoading ? (
                <p>Loading...</p>
            ) : (
                <VotingOptionsTable options={data.options} onEdit={actions.startEdit} onDelete={actions.startDelete} />
            )}

            {actions.editingOption && (
                <EditNameDialog
                    title="Edit voting option"
                    initialValue={actions.editingOption.name}
                    onSave={actions.saveEdit}
                    onCancel={actions.cancelEdit}
                />
            )}

            {actions.deletingOption && (
                <ConfirmDialog
                    title="Remove voting option"
                    message={`Are you sure you want to remove "${actions.deletingOption.name}"?`}
                    confirmLabel="Remove"
                    onConfirm={actions.confirmDelete}
                    onCancel={actions.cancelDelete}
                />
            )}

            {actions.showRemoveAllConfirm && (
                <ConfirmDialog
                    title="Remove all voting options"
                    message="Are you sure you want to remove all voting options? This cannot be undone."
                    confirmLabel="Remove all"
                    onConfirm={actions.confirmRemoveAll}
                    onCancel={actions.cancelRemoveAllConfirm}
                />
            )}
        </div>
    )
}

export default AdminVotingOptions