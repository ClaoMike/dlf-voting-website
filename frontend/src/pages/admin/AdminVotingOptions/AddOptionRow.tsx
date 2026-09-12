type AddOptionRowProps = {
    newName: string
    onNameChange: (value: string) => void
    isAdding: boolean
    onAdd: () => void
}

function AddOptionRow({ newName, onNameChange, isAdding, onAdd }: AddOptionRowProps) {
    return (
        <div className="voting-options-add-row">
            <input
                type="text"
                placeholder="New voting option name"
                value={newName}
                onChange={(e) => onNameChange(e.target.value)}
            />
            <button disabled={!newName.trim() || isAdding} onClick={onAdd}>
                Add option
            </button>
        </div>
    )
}

export default AddOptionRow