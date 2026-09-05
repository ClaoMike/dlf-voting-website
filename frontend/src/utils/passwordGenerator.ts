const UPPERCASE = 'ABCDEFGHJKLMNPQRSTUVWXYZ'
const LOWERCASE = 'abcdefghijkmnpqrstuvwxyz'
const DIGITS = '23456789'
const SPECIAL = '!@#$%^&*_-+=?'
const ALL = UPPERCASE + LOWERCASE + DIGITS + SPECIAL

function randomChar(charset: string): string {
    const index = Math.floor(Math.random() * charset.length)
    return charset[index]
}

function shuffle(chars: string[]): string[] {
    const array = [...chars]
    for (let i = array.length - 1; i > 0; i--) {
        const j = Math.floor(Math.random() * (i + 1))
        ;[array[i], array[j]] = [array[j], array[i]]
    }
    return array
}

export function generateSecurePassword(length = 24): string {
    const required = [
        randomChar(UPPERCASE),
        randomChar(DIGITS),
        randomChar(SPECIAL),
    ]

    const remainingLength = Math.max(length - required.length, 0)
    const remaining = Array.from({ length: remainingLength }, () => randomChar(ALL))

    return shuffle([...required, ...remaining]).join('')
}