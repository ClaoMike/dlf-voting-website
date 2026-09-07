import { useUserAuth } from '../context/UserAuthContext'

function Welcome() {
    const { email } = useUserAuth()
    return <h1>Welcome, {email}</h1>
}

export default Welcome