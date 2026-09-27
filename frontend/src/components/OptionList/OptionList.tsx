import { useId } from 'react'
import './OptionList.css'

type Option = {
    id: string
    name: string
}

type OptionListProps = {
    legend: string
    // For when a visible heading already names the list; screen readers still get the legend.
    hideLegend?: boolean
    hint?: string
    options: Option[]
    selectedId: string
    onChange: (id: string) => void
}

/** The ballot: one radio card per option. Native radios, so arrow keys and screen readers work as expected. */
function OptionList({ legend, hideLegend = false, hint, options, selectedId, onChange }: OptionListProps) {
    const name = useId()
    const hintId = useId()

    return (
        <fieldset className="option-list" aria-describedby={hint ? hintId : undefined}>
            <legend className={hideLegend ? 'visually-hidden' : 'option-list-legend'}>{legend}</legend>
            {hint && <p id={hintId} className="option-list-hint">{hint}</p>}

            <div className="option-list-items">
                {options.map((option) => (
                    <label
                        key={option.id}
                        className={option.id === selectedId ? 'option-card selected' : 'option-card'}
                    >
                        <input
                            type="radio"
                            name={name}
                            value={option.id}
                            checked={option.id === selectedId}
                            onChange={() => onChange(option.id)}
                        />
                        <span className="option-card-name">{option.name}</span>
                    </label>
                ))}
            </div>
        </fieldset>
    )
}

export default OptionList
