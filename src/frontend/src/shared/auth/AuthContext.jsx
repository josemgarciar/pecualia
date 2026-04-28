import { createContext, useContext, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { apiRequest } from '../api/client';

const STORAGE_KEY = 'pecualia.auth';
const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const navigate = useNavigate();
  const [token, setToken] = useState(null);
  const [user, setUser] = useState(null);
  const [bootstrapped, setBootstrapped] = useState(false);

  useEffect(() => {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      setBootstrapped(true);
      return;
    }

    const parsed = JSON.parse(raw);
    setToken(parsed.token);

    apiRequest('/api/auth/me', { token: parsed.token })
      .then((profile) => {
        setUser(profile);
      })
      .catch(() => {
        localStorage.removeItem(STORAGE_KEY);
        setToken(null);
        setUser(null);
      })
      .finally(() => {
        setBootstrapped(true);
      });
  }, []);

  const login = (authResponse) => {
    setToken(authResponse.token);
    setUser(authResponse.user);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(authResponse));
    navigate('/app/dashboard');
  };

  const logout = () => {
    localStorage.removeItem(STORAGE_KEY);
    setToken(null);
    setUser(null);
    navigate('/login');
  };

  const refreshProfile = async () => {
    if (!token) return;
    const profile = await apiRequest('/api/auth/me', { token });
    setUser(profile);
    localStorage.setItem(STORAGE_KEY, JSON.stringify({ token, user: profile }));
  };

  const value = useMemo(
    () => ({ token, user, bootstrapped, login, logout, refreshProfile }),
    [token, user, bootstrapped]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth debe usarse dentro de AuthProvider');
  }
  return context;
}
