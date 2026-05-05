import { useEffect, useMemo, useState } from 'react';
import {
  Bell,
  CreditCard,
  Globe2,
  LogOut,
  Mail,
  RefreshCw,
  Save,
  Settings,
  Shield,
  UserCircle2
} from 'lucide-react';
import { apiRequest } from '../../shared/api/client';
import { useAuth } from '../../shared/auth/AuthContext';
import { getPlanLabel } from '../../shared/subscription/plans';

const notificationStorageKey = (userId) => `pecualia.notifications.${userId}`;

function buildInitialAccountForm(user) {
  return {
    name: user?.name ?? '',
    surname: user?.surname ?? '',
    email: user?.email ?? '',
    username: user?.username ?? '',
    organizationName: user?.organizationName ?? ''
  };
}

function buildInitialPasswordForm() {
  return {
    currentPassword: '',
    newPassword: '',
    confirmPassword: ''
  };
}

function buildInitialNotifications(user) {
  if (!user?.id) {
    return {
      emailNotifications: true,
      operationalAlerts: true,
      bookReminders: true,
      accountAnnouncements: false
    };
  }

  try {
    const raw = localStorage.getItem(notificationStorageKey(user.id));
    if (!raw) {
      return {
        emailNotifications: true,
        operationalAlerts: true,
        bookReminders: true,
        accountAnnouncements: false
      };
    }

    return JSON.parse(raw);
  } catch {
    return {
      emailNotifications: true,
      operationalAlerts: true,
      bookReminders: true,
      accountAnnouncements: false
    };
  }
}

export function SettingsPage() {
  const { token, user, logout, refreshProfile } = useAuth();
  const [accountForm, setAccountForm] = useState(() => buildInitialAccountForm(user));
  const [passwordForm, setPasswordForm] = useState(buildInitialPasswordForm);
  const [notifications, setNotifications] = useState(() => buildInitialNotifications(user));
  const [accountSaving, setAccountSaving] = useState(false);
  const [passwordSaving, setPasswordSaving] = useState(false);
  const [notificationSaving, setNotificationSaving] = useState(false);
  const [feedback, setFeedback] = useState('');
  const [error, setError] = useState('');

  useEffect(() => {
    setAccountForm(buildInitialAccountForm(user));
  }, [user]);

  useEffect(() => {
    setNotifications(buildInitialNotifications(user));
  }, [user]);

  const isManager = user?.role === 'Manager';
  const planLabel = getPlanLabel(user);
  const accountSummary = useMemo(
    () => ({
      role: isManager ? 'Gestora' : 'Ganadero',
      email: user?.email ?? 'No informado',
      username: user?.username || 'Pendiente de activación'
    }),
    [isManager, user]
  );

  async function saveAccountSettings(includePasswordChange) {
    const payload = {
      name: accountForm.name,
      surname: accountForm.surname,
      email: accountForm.email,
      username: accountForm.username || null,
      organizationName: isManager ? (accountForm.organizationName || null) : null,
      currentPassword: includePasswordChange ? passwordForm.currentPassword : null,
      newPassword: includePasswordChange ? passwordForm.newPassword : null
    };

    const response = await apiRequest('/api/auth/settings', {
      method: 'PUT',
      token,
      body: payload
    });

    await refreshProfile();
    return response;
  }

  async function handleAccountSubmit(event) {
    event.preventDefault();
    setAccountSaving(true);
    setFeedback('');
    setError('');

    try {
      await saveAccountSettings(false);
      setFeedback('Datos de cuenta actualizados correctamente.');
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setAccountSaving(false);
    }
  }

  async function handlePasswordSubmit(event) {
    event.preventDefault();
    setPasswordSaving(true);
    setFeedback('');
    setError('');

    if (!passwordForm.currentPassword || !passwordForm.newPassword || !passwordForm.confirmPassword) {
      setError('Debes completar la contraseña actual y la nueva contraseña.');
      setPasswordSaving(false);
      return;
    }

    if (passwordForm.newPassword !== passwordForm.confirmPassword) {
      setError('La confirmación de contraseña no coincide.');
      setPasswordSaving(false);
      return;
    }

    try {
      await saveAccountSettings(true);
      setPasswordForm(buildInitialPasswordForm());
      setFeedback('Contraseña actualizada correctamente.');
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setPasswordSaving(false);
    }
  }

  function handleNotificationToggle(field) {
    setNotifications((current) => ({ ...current, [field]: !current[field] }));
  }

  async function handleNotificationsSubmit(event) {
    event.preventDefault();
    setNotificationSaving(true);
    setFeedback('');
    setError('');

    try {
      if (user?.id) {
        localStorage.setItem(notificationStorageKey(user.id), JSON.stringify(notifications));
      }
      setFeedback('Preferencias de notificaciones guardadas. Se aplicarán al conectar el servicio de envío.');
    } catch {
      setError('No se pudieron guardar las preferencias de notificaciones.');
    } finally {
      setNotificationSaving(false);
    }
  }

  return (
    <div className="page-stack">
      <header className="page-header">
        <div>
          <h1>Ajustes</h1>
          <p>Gestiona datos de cuenta, credenciales y preferencias de notificaciones desde una pantalla dedicada.</p>
        </div>
      </header>

      {error && <div className="error-banner">{error}</div>}
      {feedback && <div className="success-banner">{feedback}</div>}

      <section className="settings-hero-card">
        <div className="settings-hero-copy">
          <span className="settings-hero-kicker">Cuenta y preferencias</span>
          <h2>{user?.name} {user?.surname}</h2>
          <p>Actualiza correo, usuario, contraseña y, si eres gestora, la organización asociada a la cuenta. También puedes preparar la suscripción a notificaciones.</p>
        </div>
        <div className="settings-hero-actions">
          <button className="secondary-button" type="button" onClick={refreshProfile}>
            <RefreshCw size={15} />
            Recargar perfil
          </button>
          <button className="primary-button" type="button" onClick={logout}>
            <LogOut size={15} />
            Cerrar sesión
          </button>
        </div>
      </section>

      <section className="settings-summary-grid">
        <article className="settings-summary-card">
          <UserCircle2 size={18} />
          <span>Rol</span>
          <strong>{accountSummary.role}</strong>
        </article>
        <article className="settings-summary-card">
          <Mail size={18} />
          <span>Correo</span>
          <strong>{accountSummary.email}</strong>
        </article>
        <article className="settings-summary-card">
          <Shield size={18} />
          <span>Usuario</span>
          <strong>{accountSummary.username}</strong>
        </article>
        <article className="settings-summary-card">
          <CreditCard size={18} />
          <span>Suscripción</span>
          <strong>{planLabel}</strong>
        </article>
      </section>

      <div className="settings-grid">
        <form className="panel-card stack" onSubmit={handleAccountSubmit}>
          <div className="panel-header-inline">
            <div>
              <h2>Datos de cuenta</h2>
              <p>Cambia la información principal que se muestra en Pecualia.</p>
            </div>
          </div>

          <div className="grid-form">
            <label>
              <span>Nombre</span>
              <input value={accountForm.name} onChange={(event) => setAccountForm((current) => ({ ...current, name: event.target.value }))} />
            </label>
            <label>
              <span>Apellidos</span>
              <input value={accountForm.surname} onChange={(event) => setAccountForm((current) => ({ ...current, surname: event.target.value }))} />
            </label>
            <label>
              <span>Correo</span>
              <input type="email" value={accountForm.email} onChange={(event) => setAccountForm((current) => ({ ...current, email: event.target.value }))} />
            </label>
            <label>
              <span>Nombre de usuario</span>
              <input value={accountForm.username} onChange={(event) => setAccountForm((current) => ({ ...current, username: event.target.value }))} />
            </label>
            {isManager && (
              <label className="form-full">
                <span>Organización</span>
                <input value={accountForm.organizationName} onChange={(event) => setAccountForm((current) => ({ ...current, organizationName: event.target.value }))} />
              </label>
            )}
          </div>

          <div className="settings-form-actions">
            <button className="primary-button" type="submit" disabled={accountSaving}>
              <Save size={15} />
              {accountSaving ? 'Guardando...' : 'Guardar datos'}
            </button>
          </div>
        </form>

        <form className="panel-card stack" onSubmit={handlePasswordSubmit}>
          <div className="panel-header-inline">
            <div>
              <h2>Seguridad</h2>
              <p>Actualiza la contraseña de acceso a la aplicación.</p>
            </div>
          </div>

          <div className="grid-form">
            <label className="form-full">
              <span>Contraseña actual</span>
              <input type="password" value={passwordForm.currentPassword} onChange={(event) => setPasswordForm((current) => ({ ...current, currentPassword: event.target.value }))} />
            </label>
            <label>
              <span>Nueva contraseña</span>
              <input type="password" value={passwordForm.newPassword} onChange={(event) => setPasswordForm((current) => ({ ...current, newPassword: event.target.value }))} />
            </label>
            <label>
              <span>Confirmar contraseña</span>
              <input type="password" value={passwordForm.confirmPassword} onChange={(event) => setPasswordForm((current) => ({ ...current, confirmPassword: event.target.value }))} />
            </label>
          </div>

          <div className="settings-security-note">
            <Shield size={16} />
            <span>Para cambiar la contraseña es obligatorio confirmar la contraseña actual.</span>
          </div>

          <div className="settings-form-actions">
            <button className="primary-button" type="submit" disabled={passwordSaving}>
              <Shield size={15} />
              {passwordSaving ? 'Actualizando...' : 'Actualizar contraseña'}
            </button>
          </div>
        </form>
      </div>

      <form className="panel-card stack" onSubmit={handleNotificationsSubmit}>
        <div className="panel-header-inline">
          <div>
            <h2>Notificaciones</h2>
            <p>Define qué comunicaciones quieres recibir o tener activas cuando se conecte el servicio de envío.</p>
          </div>
        </div>

        <div className="settings-notification-grid">
          <button type="button" className={notifications.emailNotifications ? 'settings-toggle-card settings-toggle-card-active' : 'settings-toggle-card'} onClick={() => handleNotificationToggle('emailNotifications')}>
            <Mail size={18} />
            <div>
              <strong>Notificaciones por correo</strong>
              <span>Habilita el envío general al correo principal de la cuenta.</span>
            </div>
          </button>

          <button type="button" className={notifications.operationalAlerts ? 'settings-toggle-card settings-toggle-card-active' : 'settings-toggle-card'} onClick={() => handleNotificationToggle('operationalAlerts')}>
            <Bell size={18} />
            <div>
              <strong>Avisos operativos</strong>
              <span>Incidencias, movimientos y eventos relevantes de la operativa diaria.</span>
            </div>
          </button>

          <button type="button" className={notifications.bookReminders ? 'settings-toggle-card settings-toggle-card-active' : 'settings-toggle-card'} onClick={() => handleNotificationToggle('bookReminders')}>
            <Settings size={18} />
            <div>
              <strong>Recordatorios documentales</strong>
              <span>Alertas para libro, inspecciones o revisiones pendientes.</span>
            </div>
          </button>

          <button type="button" className={notifications.accountAnnouncements ? 'settings-toggle-card settings-toggle-card-active' : 'settings-toggle-card'} onClick={() => handleNotificationToggle('accountAnnouncements')}>
            <Globe2 size={18} />
            <div>
              <strong>Novedades de cuenta</strong>
              <span>Comunicaciones sobre mejoras del producto, facturación y cambios de servicio.</span>
            </div>
          </button>
        </div>

        <div className="settings-security-note">
          <Bell size={16} />
          <span>Estas preferencias quedan guardadas en local hasta conectar el envío real de notificaciones en backend.</span>
        </div>

        <div className="settings-form-actions">
          <button className="primary-button" type="submit" disabled={notificationSaving}>
            <Save size={15} />
            {notificationSaving ? 'Guardando...' : 'Guardar notificaciones'}
          </button>
        </div>
      </form>
    </div>
  );
}
