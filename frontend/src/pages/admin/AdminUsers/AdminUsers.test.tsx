import { cleanup, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import AdminUsers from './AdminUsers'
import type { PagedUsers, User } from './types'

const existingUser: User = {
    id: '11111111-1111-1111-1111-111111111111',
    username: 'existing-user',
    email: null,
    employeeCode: null,
    firstName: null,
    lastName: null,
    companyCode: null,
    employmentDate: null,
    electability: null,
    createdAt: '2026-09-01T00:00:00Z',
}

const usersPage: PagedUsers = {
    items: [{ id: existingUser.id, name: null, username: existingUser.username }],
    totalCount: 1,
    page: 1,
    pageSize: 25,
}

const json = (body: unknown) => new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })

// Stands in for the API; every create/update request body is kept so the tests can check the password sent.
function mockApi() {
    const sentBodies: { method: string; body: { password: string | null } }[] = []
    vi.stubGlobal('fetch', vi.fn(async (url: string, init?: RequestInit) => {
        const method = init?.method ?? 'GET'
        if (method === 'POST' || method === 'PUT') {
            sentBodies.push({ method, body: JSON.parse(init!.body as string) })
            return json(existingUser)
        }
        if (url.startsWith('/api/users?')) return json(usersPage)
        if (url === `/api/users/${existingUser.id}`) return json(existingUser)
        return new Response(null, { status: 404 })
    }))
    return sentBodies
}

// The dialog's password input; revealed so its value can be read like the admin sees it.
async function generatePassword(user: ReturnType<typeof userEvent.setup>, dialog: HTMLElement) {
    await user.click(within(dialog).getByRole('button', { name: 'Generate secure password' }))
    await user.click(within(dialog).getByRole('button', { name: 'Show password' }))
    return within(dialog).getByLabelText(/password/i, { selector: 'input' }) as HTMLInputElement
}

describe('Generated passwords on the Users page', () => {
    let sentBodies: ReturnType<typeof mockApi>

    beforeEach(() => {
        sentBodies = mockApi()
    })

    afterEach(() => {
        cleanup()
        vi.unstubAllGlobals()
    })

    it('are 8 characters when adding a user', async () => {
        const user = userEvent.setup()
        render(<AdminUsers />)
        await screen.findByRole('button', { name: /^Edit\s*existing-user$/ })

        await user.click(screen.getByRole('button', { name: 'Add user' }))
        const dialog = screen.getByRole('dialog')
        await user.type(within(dialog).getByLabelText('Username'), 'new-voter')
        const input = await generatePassword(user, dialog)

        expect(input.value).toHaveLength(8)

        await user.click(within(dialog).getByRole('button', { name: 'Create user' }))
        expect(sentBodies).toHaveLength(1)
        expect(sentBodies[0].method).toBe('POST')
        expect(sentBodies[0].body.password).toHaveLength(8)
        expect(sentBodies[0].body.password).toBe(input.value)
    })

    it('are 8 characters when editing a user', async () => {
        const user = userEvent.setup()
        render(<AdminUsers />)
        await screen.findByRole('button', { name: /^Edit\s*existing-user$/ })

        await user.click(screen.getByRole('button', { name: /^Edit\s*existing-user$/ }))
        const dialog = await screen.findByRole('dialog')
        const input = await generatePassword(user, dialog)

        expect(input.value).toHaveLength(8)

        await user.click(within(dialog).getByRole('button', { name: 'Save' }))
        expect(sentBodies).toHaveLength(1)
        expect(sentBodies[0].method).toBe('PUT')
        expect(sentBodies[0].body.password).toHaveLength(8)
    })

    it('are accepted by the dialog every time', async () => {
        const user = userEvent.setup()
        render(<AdminUsers />)
        await screen.findByRole('button', { name: /^Edit\s*existing-user$/ })

        await user.click(screen.getByRole('button', { name: 'Add user' }))
        const dialog = screen.getByRole('dialog')
        await user.type(within(dialog).getByLabelText('Username'), 'new-voter')

        // Generated passwords are random, so try a batch: none may be the wrong length or fail the requirements.
        const generate = within(dialog).getByRole('button', { name: 'Generate secure password' })
        const input = within(dialog).getByLabelText(/password/i, { selector: 'input' }) as HTMLInputElement
        for (let i = 0; i < 50; i++) {
            await user.click(generate)
            expect(input.value).toHaveLength(8)
            expect(within(dialog).queryByText('This password does not meet the requirements above.')).toBeNull()
            expect((within(dialog).getByRole('button', { name: 'Create user' }) as HTMLButtonElement).disabled).toBe(false)
        }
    })
})
