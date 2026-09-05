import { useState } from 'react'
import { generateSecurePassword } from '../utils/passwordGenerator'
import './PasswordField.css'

type PasswordFieldProps = {
    value: string
    onChange: (value: string) => void
    placeholder?: string
}

function PasswordField({ value, onChange, placeholder }: PasswordFieldProps) {
    const [visible, setVisible] = useState(false)

    return (
        <div className="password-field">
            <input
                type={visible ? 'text' : 'password'}
                value={value}
                onChange={(e) => onChange(e.target.value)}
                placeholder={placeholder ?? 'Password'}
            />
            <button
                type="button"
                className="password-field-toggle"
                onClick={() => setVisible((v) => !v)}
            >
                {visible ? 'Hide' : 'Show'}
            </button>
            <button
                type="button"
                className="password-field-generate"
                onClick={() => onChange(generateSecurePassword())}
            >
                Generate secure password
            </button>
        </div>
    )
}

export default PasswordField