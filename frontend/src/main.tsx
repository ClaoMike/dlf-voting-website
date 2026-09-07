import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import './index.css'
import App from './App.tsx'
import { AdminAuthProvider } from './context/AdminAuthContext'
import { UserAuthProvider } from './context/UserAuthContext'

createRoot(document.getElementById('root')!).render(
    <StrictMode>
        <BrowserRouter>
            <AdminAuthProvider>
                <UserAuthProvider>
                    <App />
                </UserAuthProvider>
            </AdminAuthProvider>
        </BrowserRouter>
    </StrictMode>,
)