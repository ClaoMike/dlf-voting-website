import type { SubmitEvent } from 'react'

type AddOptionRowProps = {
    newName: string
    onNameChange: (value: string) => void
    isAdding: boolean
    onAdd: () => void
}

function AddOptionRow({ newName, onNameChange, isAdding, onAdd }: AddOptionRowProps) {
    // A form, so Enter in the field adds the option.
    const handleSubmit = (e: SubmitEvent<HTMLFormElement>) => {
        e.preventDefault()
        if (newName.trim() && !isAdding) onAdd()
    }

    return (
        <form className="voting-options-add-row" onSubmit={handleSubmit}>
            <label htmlFor="new-voting-option" className="visually-hidden">New voting option name</label>
            <input
                id="new-voting-option"
                type="text"
                placeholder="New voting option name"
                value={newName}
                onChange={(e) => onNameChange(e.target.value)}
            />
            <button type="submit" disabled={!newName.trim() || isAdding}>
                Add option
            </button>
        </form>
    )
}

export default AddOptionRow
