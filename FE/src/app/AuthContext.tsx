import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { session, type User } from '../api/http';
import { identityApi } from '../api/identity';
type Login = { userName: string; password: string; code?: string; recoveryCode?: string };
type Auth = {
  user: User | null;
  verificationError?: Error;
  signIn: (input: Login) => Promise<void>;
  signOut: (all?: boolean) => Promise<void>;
};
const Context = createContext<Auth | null>(null);
export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [verificationError, setVerificationError] = useState<Error>();
  const userId = user?.id;
  useEffect(
    () =>
      session.subscribe(() => {
        if (!session.get()) setUser(null);
      }),
    [],
  );
  useEffect(() => {
    if (!userId) return;
    const controller = new AbortController();
    const check = () => {
      if (document.visibilityState === 'visible')
        identityApi
          .me(controller.signal)
          .then((value) => {
            setUser(value);
            setVerificationError(undefined);
          })
          .catch((error) => {
            if (!controller.signal.aborted)
              setVerificationError(
                error instanceof Error ? error : new Error('Không xác minh được phiên.'),
              );
          });
    };
    const timer = setInterval(check, 60000);
    document.addEventListener('visibilitychange', check);
    return () => {
      clearInterval(timer);
      controller.abort();
      document.removeEventListener('visibilitychange', check);
    };
  }, [userId]);
  async function signIn(input: Login) {
    const grant = await identityApi.login(input);
    session.set(grant);
    try {
      setUser(await identityApi.me());
      setVerificationError(undefined);
    } catch (error) {
      session.set(null);
      throw error;
    }
  }
  async function signOut(all = false) {
    await identityApi.logout(all);
    session.set(null);
    setUser(null);
  }
  return (
    <Context.Provider value={{ user, verificationError, signIn, signOut }}>
      {children}
    </Context.Provider>
  );
}
export function useAuth() {
  const auth = useContext(Context);
  if (!auth) throw new Error('AuthProvider is required.');
  return auth;
}
