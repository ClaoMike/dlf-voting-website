export const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

// Mirrors the backend: 20-64 chars, at least one uppercase, one digit, one special char.
export const PASSWORD_REGEX = /^(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{20,64}$/

export function isValidEmail(email: string): boolean {
    return EMAIL_REGEX.test(email.trim())
}

export function isValidPassword(password: string): boolean {
    return PASSWORD_REGEX.test(password)
}