import { useId, useState } from 'react'
import { generateSecurePassword } from '../../utils/passwordGenerator'

import './PasswordField.css'

type PasswordFieldProps = {
    value: string
    onChange: (value: string) => void
    label?: string
    hint?: string
    generatedLength?: number
}

function PasswordField({ value, onChange, label = 'Password', hint, generatedLength }: PasswordFieldProps) {
    const [visible, setVisible] = useState(false)
    const id = useId()
    const hintId = useId()

    return (
        <div className="password-field-group">
            <label className="dialog-label" htmlFor={id}>{label}</label>
            {hint && <p id={hintId} className="dialog-hint">{hint}</p>}
            <div className="password-field">
                <input
                    id={id}
                    type={visible ? 'text' : 'password'}
                    autoComplete="new-password"
                    value={value}
                    onChange={(e) => onChange(e.target.value)}
                    aria-describedby={hint ? hintId : undefined}
                />
                <button
                    type="button"
                    className="password-field-toggle"
                    onClick={() => setVisible((v) => !v)}
                    aria-controls={id}
                    aria-label={visible ? 'Hide password' : 'Show password'}
                >
                    {visible ? 'Hide' : 'Show'}
                </button>
                <button
                    type="button"
                    className="password-field-generate"
                    onClick={() => onChange(generateSecurePassword(generatedLength))}
                >
                    Generate secure password
                </button>
            </div>
        </div>
    )
}

export default PasswordField
