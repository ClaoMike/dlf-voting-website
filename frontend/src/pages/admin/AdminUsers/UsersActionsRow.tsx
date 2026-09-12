import type { RefObject, ChangeEvent } from 'react'

type UsersActionsRowProps = {
    onAddUser: () => void
    onImportClick: () => void
    isImporting: boolean
    fileInputRef: RefObject<HTMLInputElement | null>
    onFileSelected: (e: ChangeEvent<HTMLInputElement>) => void
    canRemoveAll: boolean
    onRemoveAllClick: () => void
}

function UsersActionsRow({
                             onAddUser,
                             onImportClick,
                             isImporting,
                             fileInputRef,
                             onFileSelected,
                             canRemoveAll,
                             onRemoveAllClick,
                         }: UsersActionsRowProps) {
    return (
        <div className="users-actions-row">
            <button className="voting-options-add-row-button" onClick={onAddUser}>
                Add user
            </button>

            <button className="voting-options-add-row-button" onClick={onImportClick} disabled={isImporting}>
                {isImporting ? 'Importing...' : 'Import users from CSV'}
            </button>

            <input
                type="file"
                accept=".csv,text/csv"
                ref={fileInputRef}
                onChange={onFileSelected}
                style={{ display: 'none' }}
            />

            <button className="voting-options-remove-all" disabled={!canRemoveAll} onClick={onRemoveAllClick}>
                Remove all
            </button>
        </div>
    )
}

export default UsersActionsRow