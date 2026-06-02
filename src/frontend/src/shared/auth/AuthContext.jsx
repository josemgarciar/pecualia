import { createContext, useContext, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { apiRequest } from '../api/client';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const navigate = useNavigate();
  const [user, setUser] = useState(null);
  const [bootstrapped, setBootstrapped] = useState(false);

  useEffect(() => {
    apiRequest('/api/auth/me')
      .then((profile) => {
        setUser(profile);
      })
      .catch(() => {
        setUser(null);
      })
      .finally(() => {
        setBootstrapped(true);
      });
  }, []);

  const login = (authResponse) => {
    setUser(authResponse.user);
    navigate('/app/dashboard');
  };

  const logout = async () => {
    try {
      await apiRequest('/api/auth/logout', { method: 'POST' });
    } catch {
      // Limpia el estado local aunque el backend ya no esté disponible.
    }

    setUser(null);
    navigate('/login', { replace: true });
  };

  const refreshProfile = async () => {
    const profile = await apiRequest('/api/auth/me');
    setUser(profile);
  };

  const deleteAccount = async () => {
    await apiRequest('/api/auth/me', { method: 'DELETE' });
    setUser(null);
    navigate('/login', { replace: true });
  };

  const value = useMemo(
    () => ({ user, bootstrapped, login, logout, refreshProfile, deleteAccount }),
    [user, bootstrapped]
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
