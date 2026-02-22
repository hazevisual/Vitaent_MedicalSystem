import { createContext, useContext, useMemo, useState } from 'react';

type AuthUser = {
  id: string;
  email: string;
  role: string;
};

type AuthContextValue = {
  accessToken: string | null;
  user: AuthUser | null;
  setSession: (token: string, user: AuthUser) => void;
  updateToken: (token: string) => void;
  clearSession: () => void;
};

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [accessToken, setAccessToken] = useState<string | null>(null);
  const [user, setUser] = useState<AuthUser | null>(null);

  const value = useMemo<AuthContextValue>(
    () => ({
      accessToken,
      user,
      setSession: (token, authUser) => {
        setAccessToken(token);
        setUser(authUser);
      },
      updateToken: (token) => setAccessToken(token),
      clearSession: () => {
        setAccessToken(null);
        setUser(null);
      }
    }),
    [accessToken, user]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within AuthProvider');
  }
  return context;
}

export type { AuthUser };
