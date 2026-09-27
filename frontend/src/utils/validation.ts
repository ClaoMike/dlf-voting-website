export const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

// Mirrors the backend: 20-64 chars, at least one uppercase, one digit, one special char.
export const PASSWORD_REGEX = /^(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{20,64}$/

// Mirrors the backend: no whitespace or control characters; anything else (including @ and æ/ø/å) is allowed.
const USERNAME_CHARS_REGEX = /^[^\s\p{C}]*$/u

export const USERNAME_MIN_LENGTH = 5
export const USER_USERNAME_MAX_LENGTH = 20
export const ADMIN_USERNAME_MAX_LENGTH = 320

export function isValidEmail(email: string): boolean {
    return EMAIL_REGEX.test(email.trim())
}

export function isValidPassword(password: string): boolean {
    return PASSWORD_REGEX.test(password)
}

export function isValidUsername(username: string, maxLength: number): boolean {
    const trimmed = username.trim()
    return trimmed.length >= USERNAME_MIN_LENGTH && trimmed.length <= maxLength && USERNAME_CHARS_REGEX.test(trimmed)
}
