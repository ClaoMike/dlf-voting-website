import type { VotingOption } from './types'

type VotingOptionsTableProps = {
    options: VotingOption[]
    onEdit: (option: VotingOption) => void
    onDelete: (option: VotingOption) => void
}

function VotingOptionsTable({ options, onEdit, onDelete }: VotingOptionsTableProps) {
    return (
        <table className="voting-options-table">
            <thead>
            <tr>
                <th>Name</th>
                <th>Created</th>
                <th></th>
            </tr>
            </thead>
            <tbody>
            {options.map((option) => (
                <tr key={option.id}>
                    <td>{option.name}</td>
                    <td>{new Date(option.createdAt).toLocaleDateString()}</td>
                    <td className="voting-options-actions">
                        <button className="voting-options-edit" onClick={() => onEdit(option)}>
                            Edit
                        </button>
                        <button className="voting-options-remove" onClick={() => onDelete(option)}>
                            Remove
                        </button>
                    </td>
                </tr>
            ))}
            </tbody>
        </table>
    )
}

export default VotingOptionsTable