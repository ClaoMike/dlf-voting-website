import { useEffect } from 'react'

// Sessions end after 10 minutes without activity (see SessionValidation in the backend); every API call renews them.
// This covers activity that makes no API call, like reading the ballot. Once a minute at most is plenty.
const MIN_INTERVAL_MS = 60_000

const ACTIVITY_EVENTS = ['pointerdown', 'keydown', 'touchstart', 'scroll'] as const

/**
 * While signed in, renews the session on user activity (click, key press, touch, scroll) by calling `refreshUrl`,
 * at most once a minute. Calls `onExpired` if the server says the session has already ended.
 */
export function useSessionKeepAlive(isSignedIn: boolean, refreshUrl: string, onExpired: () => void) {
    useEffect(() => {
        if (!isSignedIn) return

        let lastRefresh = Date.now()
        let inFlight = false

        const onActivity = () => {
            if (inFlight || Date.now() - lastRefresh < MIN_INTERVAL_MS) return

            inFlight = true
            lastRefresh = Date.now()
            fetch(refreshUrl, { method: 'POST', credentials: 'include' })
                .then((res) => {
                    if (res.status === 401) onExpired()
                })
                .catch(() => {
                    // Offline for a moment: the next activity after the interval tries again.
                })
                .finally(() => {
                    inFlight = false
                })
        }

        for (const event of ACTIVITY_EVENTS) {
            window.addEventListener(event, onActivity, { capture: true, passive: true })
        }
        return () => {
            for (const event of ACTIVITY_EVENTS) {
                window.removeEventListener(event, onActivity, { capture: true })
            }
        }
    }, [isSignedIn, refreshUrl, onExpired])
}
