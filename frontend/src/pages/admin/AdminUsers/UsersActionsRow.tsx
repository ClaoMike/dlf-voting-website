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
        <div className="admin-card-toolbar">
            <button className="voting-options-add-row-button" onClick={onAddUser}>
                Add user
            </button>

            <button className="voting-options-add-row-button" onClick={onImportClick} disabled={isImporting}>
                {isImporting ? 'Importing...' : 'Import users from Excel'}
            </button>

            <input
                type="file"
                accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                ref={fileInputRef}
                onChange={onFileSelected}
                hidden
                aria-hidden="true"
                tabIndex={-1}
            />

            <button className="voting-options-remove-all" disabled={!canRemoveAll} onClick={onRemoveAllClick}>
                Remove all
            </button>
        </div>
    )
}

export default UsersActionsRow