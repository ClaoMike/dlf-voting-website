import { useRef, useState } from 'react'

const API_BASE = 'http://localhost:5120/api/users'

function escapeCsvField(value: string) {
    if (value.includes(',') || value.includes('"') || value.includes('\n')) {
        return `"${value.replace(/"/g, '""')}"`
    }
    return value
}

function downloadCsv(rows: { email: string; password: string }[]) {
    const header = 'email,password'
    const lines = rows.map((r) => `${escapeCsvField(r.email)},${escapeCsvField(r.password)}`)
    const csvContent = [header, ...lines].join('\n')

    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `imported-users-${new Date().toISOString().slice(0, 10)}.csv`
    link.click()
    URL.revokeObjectURL(url)
}

export function useUserImport(onChanged: () => Promise<void>) {
    const [isImporting, setIsImporting] = useState(false)
    const [importSummary, setImportSummary] = useState<{ created: number; skipped: number } | null>(null)
    const [error, setError] = useState<string | null>(null)
    const fileInputRef = useRef<HTMLInputElement>(null)

    const handleFileSelected = async (e: React.ChangeEvent<HTMLInputElement>) => {
        const file = e.target.files?.[0]
        if (!file) return

        setIsImporting(true)
        setError(null)
        setImportSummary(null)

        try {
            const formData = new FormData()
            formData.append('file', file)

            const res = await fetch(`${API_BASE}/bulk-import`, {
                method: 'POST',
                credentials: 'include',
                body: formData,
            })

            if (!res.ok) {
                const body = await res.json().catch(() => null)
                setError(body?.message ?? 'Failed to import users.')
                return
            }

            const data: { created: { email: string; password: string }[]; skipped: { email: string; reason: string }[] } =
                await res.json()

            setImportSummary({ created: data.created.length, skipped: data.skipped.length })

            if (data.created.length > 0) {
                downloadCsv(data.created)
            }

            await onChanged()
        } catch {
            setError('Failed to import users.')
        } finally {
            setIsImporting(false)
            if (fileInputRef.current) fileInputRef.current.value = ''
        }
    }

    return {
        isImporting,
        importSummary,
        error,
        fileInputRef,
        triggerFilePicker: () => fileInputRef.current?.click(),
        handleFileSelected,
    }
}