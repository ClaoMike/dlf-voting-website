import { useCallback, useEffect, useRef, useState } from 'react'

// Must match SessionValidation.Lifetime in the backend: a session ends after this long without activity.
const SESSION_LIFETIME_MS = 10 * 60_000
// How long before the end the "Are you still there?" warning appears (WCAG 2.2.1 asks for at least 20 seconds).
const WARNING_BEFORE_END_MS = 2 * 60_000
// Activity renews the session at most this often.
const MIN_REFRESH_INTERVAL_MS = 60_000

const ACTIVITY_EVENTS = ['pointerdown', 'keydown', 'touchstart', 'scroll'] as const

type SessionTimeoutOptions = {
    isSignedIn: boolean
    refreshUrl: string
    logoutUrl: string
    onExpired: () => void
}

/**
 * Keeps a signed-in session alive while the person is active, warns them before it ends, and ends it when it runs out.
 *
 * The browser counts from the last login or refresh it knows of. Other API calls renew the session too, so the server's
 * end time is never earlier than this one: the warning and the sign-out can only come early, never late.
 */
export function useSessionTimeout({ isSignedIn, refreshUrl, logoutUrl, onExpired }: SessionTimeoutOptions) {
    const [endsAt, setEndsAt] = useState(0)
    const [now, setNow] = useState(() => Date.now())
    const lastRefresh = useRef(0)
    const inFlight = useRef(false)

    /** Call when the session was just started or renewed (login, or the session check on page load). */
    const markRenewed = useCallback(() => {
        const time = Date.now()
        lastRefresh.current = time
        setEndsAt(time + SESSION_LIFETIME_MS)
        setNow(time)
    }, [])

    const refresh = useCallback(async () => {
        if (inFlight.current) return
        inFlight.current = true
        lastRefresh.current = Date.now()
        try {
            const res = await fetch(refreshUrl, { method: 'POST', credentials: 'include' })
            if (res.ok) markRenewed()
            else if (res.status === 401) onExpired()
        } catch {
            // Offline for a moment: the next activity or "Stay signed in" tries again.
        } finally {
            inFlight.current = false
        }
    }, [refreshUrl, onExpired, markRenewed])

    // Activity renews the session (at most once a minute), covering reading and scrolling that make no API call.
    useEffect(() => {
        if (!isSignedIn) return

        const onActivity = (e: Event) => {
            // Inside the warning only its buttons decide: a stray refresh could otherwise race "Sign out".
            if (e.target instanceof Element && e.target.closest('.session-warning')) return
            if (Date.now() - lastRefresh.current >= MIN_REFRESH_INTERVAL_MS) void refresh()
        }

        for (const event of ACTIVITY_EVENTS) {
            window.addEventListener(event, onActivity, { capture: true, passive: true })
        }
        return () => {
            for (const event of ACTIVITY_EVENTS) {
                window.removeEventListener(event, onActivity, { capture: true })
            }
        }
    }, [isSignedIn, refresh])

    // A one-second clock for the countdown.
    useEffect(() => {
        if (!isSignedIn) return
        const timer = setInterval(() => setNow(Date.now()), 1000)
        return () => clearInterval(timer)
    }, [isSignedIn])

    const remainingMs = endsAt - now
    const hasEnded = isSignedIn && endsAt > 0 && remainingMs <= 0

    useEffect(() => {
        if (!hasEnded) return
        // The server may allow a few more seconds; end it there too so the cookie can't be reused.
        void fetch(logoutUrl, { method: 'POST', credentials: 'include' }).catch(() => {})
        onExpired()
    }, [hasEnded, logoutUrl, onExpired])

    const warningSecondsLeft =
        isSignedIn && endsAt > 0 && remainingMs > 0 && remainingMs <= WARNING_BEFORE_END_MS
            ? Math.ceil(remainingMs / 1000)
            : null

    return { markRenewed, staySignedIn: refresh, warningSecondsLeft }
}
