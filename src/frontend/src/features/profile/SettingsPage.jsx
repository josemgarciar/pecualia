import { useEffect, useMemo, useState } from 'react';
import {
  Bell,
  CreditCard,
  AlertTriangle,
  Mail,
  RefreshCw,
  Save,
  Shield,
  UserCircle2
} from 'lucide-react';
import { apiRequest } from '../../shared/api/client';
import { useAuth } from '../../shared/auth/AuthContext';
import { ModalBody, ModalDialog, ModalFooter, ModalHeader } from '../../shared/components/modal/Modal';
import { getPlanLabel } from '../../shared/subscription/plans';

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

function buildInitialReminderForm() {
  return {
    enabled: false,
    email: '',
    intervalDays: ''
  };
}

const MIN_PASSWORD_LENGTH = 10;

function mapReminderSettings(response) {
  return {
    enabled: Boolean(response?.enabled),
    email: response?.email ?? '',
    intervalDays: response?.intervalDays ? String(response.intervalDays) : ''
  };
}

export function SettingsPage() {
  const { user, refreshProfile, deleteAccount } = useAuth();
  const [accountForm, setAccountForm] = useState(() => buildInitialAccountForm(user));
  const [passwordForm, setPasswordForm] = useState(buildInitialPasswordForm);
  const [reminderForm, setReminderForm] = useState(buildInitialReminderForm);
  const [accountSaving, setAccountSaving] = useState(false);
  const [passwordSaving, setPasswordSaving] = useState(false);
  const [reminderLoading, setReminderLoading] = useState(false);
  const [reminderSaving, setReminderSaving] = useState(false);
  const [deleteSaving, setDeleteSaving] = useState(false);
  const [deleteModalOpen, setDeleteModalOpen] = useState(false);
  const [deleteConfirmationValue, setDeleteConfirmationValue] = useState('');
  const [feedback, setFeedback] = useState('');
  const [error, setError] = useState('');

  useEffect(() => {
    setAccountForm(buildInitialAccountForm(user));
  }, [user]);

  useEffect(() => {
    let cancelled = false;

    setReminderLoading(true);

    apiRequest('/api/auth/task-reminder-settings')
      .then((response) => {
        if (!cancelled) {
          setReminderForm(mapReminderSettings(response));
        }
      })
      .catch((requestError) => {
        if (!cancelled) {
          setError(requestError.message);
        }
      })
      .finally(() => {
        if (!cancelled) {
          setReminderLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, []);

  const isManager = user?.role === 'Manager';
  const planLabel = getPlanLabel(user);
  const deleteConfirmationTarget = user?.username?.trim() ?? '';
  const deleteConfirmationMatches = deleteConfirmationValue.trim() === deleteConfirmationTarget;
  const accountSummary = useMemo(
    () => ({
      role: isManager ? 'Gestor@' : 'Ganader@',
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

    if (passwordForm.newPassword.length < MIN_PASSWORD_LENGTH) {
      setError(`La nueva contraseña debe tener al menos ${MIN_PASSWORD_LENGTH} caracteres.`);
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

  function handleReminderChange(field, value) {
    setReminderForm((current) => ({ ...current, [field]: value }));
  }

  async function handleReminderSubmit(event) {
    event.preventDefault();
    setReminderSaving(true);
    setFeedback('');
    setError('');

    const normalizedEmail = reminderForm.email.trim().toLowerCase();
    const normalizedIntervalDays = reminderForm.intervalDays.trim();

    if (reminderForm.enabled && !normalizedEmail) {
      setError('Debes indicar el correo al que quieres enviar los recordatorios.');
      setReminderSaving(false);
      return;
    }

    if (reminderForm.enabled && !normalizedIntervalDays) {
      setError('Debes indicar cada cuántos días quieres recibir recordatorios.');
      setReminderSaving(false);
      return;
    }

    if (normalizedIntervalDays && Number(normalizedIntervalDays) <= 0) {
      setError('La frecuencia de recordatorios debe ser mayor que 0 días.');
      setReminderSaving(false);
      return;
    }

    try {
      const response = await apiRequest('/api/auth/task-reminder-settings', {
        method: 'PUT',
        body: {
          enabled: reminderForm.enabled,
          email: normalizedEmail || null,
          intervalDays: normalizedIntervalDays ? Number(normalizedIntervalDays) : null
        }
      });

      setReminderForm(mapReminderSettings(response));
      setFeedback('Configuración de recordatorios guardada correctamente.');
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setReminderSaving(false);
    }
  }

  function openDeleteModal() {
    setDeleteConfirmationValue('');
    setDeleteModalOpen(true);
  }

  function closeDeleteModal() {
    if (deleteSaving) {
      return;
    }

    setDeleteModalOpen(false);
    setDeleteConfirmationValue('');
  }

  async function handleDeleteAccount() {
    if (!deleteConfirmationMatches) {
      setError('Debes escribir tu nombre de usuario para confirmar la eliminación.');
      return;
    }

    setDeleteSaving(true);
    setFeedback('');
    setError('');

    try {
      await deleteAccount();
    } catch (requestError) {
      setError(requestError.message);
      setDeleteSaving(false);
    }
  }

  return (
    <div className="page-stack">
      <header className="page-header">
        <div>
          <h1>Ajustes</h1>
          <p>Gestiona datos de cuenta, credenciales y recordatorios automáticos desde una pantalla dedicada.</p>
        </div>
      </header>

      {error && <div className="error-banner">{error}</div>}
      {feedback && <div className="success-banner">{feedback}</div>}

      <section className="settings-hero-card">
        <div className="settings-hero-copy">
          <span className="settings-hero-kicker">Cuenta y preferencias</span>
          <h2>{user?.name} {user?.surname}</h2>
        </div>
        <div className="settings-hero-actions">
          <button className="secondary-button" type="button" onClick={refreshProfile}>
            <RefreshCw size={15} />
            Recargar perfil
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

      <form className="panel-card stack settings-reminder-panel" onSubmit={handleReminderSubmit}>
        <div className="panel-header-inline">
          <div>
            <h2>Recordatorios de tareas</h2>
            <p>Recibe por correo el mismo resumen de tareas pendientes que ves en el dashboard con la frecuencia que prefieras.</p>
          </div>
        </div>

        <label className="settings-reminder-toggle">
          <input
            type="checkbox"
            checked={reminderForm.enabled}
            onChange={(event) => handleReminderChange('enabled', event.target.checked)}
          />
          <div>
            <strong>Activar recordatorios por correo</strong>
            <span>Cuando esté activo, Pecualia enviará automáticamente un resumen de tareas pendientes.</span>
          </div>
        </label>

        <div className="settings-reminder-grid">
          <label>
            <span>Correo de destino</span>
            <input
              type="email"
              placeholder="avisos@empresa.com"
              value={reminderForm.email}
              disabled={reminderLoading || !reminderForm.enabled}
              onChange={(event) => handleReminderChange('email', event.target.value)}
            />
          </label>

          <label>
            <span>Cada cuántos días</span>
            <input
              type="number"
              min="1"
              step="1"
              placeholder="7"
              value={reminderForm.intervalDays}
              disabled={reminderLoading || !reminderForm.enabled}
              onChange={(event) => handleReminderChange('intervalDays', event.target.value)}
            />
          </label>
        </div>

        <div className="settings-security-note">
          <Bell size={16} />
          <span>
            {reminderLoading
              ? 'Cargando configuración de recordatorios...'
              : reminderForm.enabled
                ? 'El primer ciclo empezará a contar desde el momento en que guardes esta configuración.'
                : 'Cuando vuelvas a activarlos, Pecualia conservará el correo y la frecuencia que hayas guardado.'}
          </span>
        </div>

        <div className="settings-form-actions">
          <button className="primary-button" type="submit" disabled={reminderLoading || reminderSaving}>
            <Save size={15} />
            {reminderSaving ? 'Guardando...' : 'Guardar recordatorios'}
          </button>
        </div>
      </form>

      <section className="panel-card stack settings-danger-panel">
        <div className="panel-header-inline">
          <div>
            <h2>Eliminar cuenta</h2>
            <p>
              {isManager
                ? 'Al eliminar tu cuenta de gestor también se eliminarán las cuentas de ganadero no activadas asociadas y se desvincularán las activas.'
                : 'Esta acción eliminará tu cuenta y no se puede deshacer.'}
            </p>
          </div>
        </div>

        <div className="settings-danger-note">
          <AlertTriangle size={16} />
          <span>
            {isManager
              ? 'Revisa antes de continuar: los ganaderos activos seguirán existiendo, pero dejarán de estar vinculados a tu gestoría.'
              : 'Si continúas, perderás el acceso a tu cuenta y a los datos que dependan de ella.'}
          </span>
        </div>

        <div className="settings-form-actions">
          <button className="danger-button" type="button" onClick={openDeleteModal} disabled={deleteSaving}>
            <AlertTriangle size={15} />
            {deleteSaving ? 'Eliminando...' : 'Eliminar cuenta'}
          </button>
        </div>
      </section>

      {deleteModalOpen && (
        <ModalDialog shellClassName="settings-delete-modal">
          <ModalHeader
            icon={<AlertTriangle size={18} />}
            title="Confirmar eliminación de cuenta"
            subtitle="Esta acción es irreversible."
            onClose={closeDeleteModal}
          />
          <ModalBody className="settings-delete-modal-body">
            <div className="settings-danger-note">
              <AlertTriangle size={16} />
              <span>
                {isManager
                  ? 'Se eliminará tu cuenta de gestor. También se borrarán los ganaderos vinculados que sigan sin activar su cuenta.'
                  : 'Se eliminará tu cuenta y dejarás de tener acceso a Pecualia.'}
              </span>
            </div>

            <div className="stack">
              <p className="settings-delete-modal-copy">
                Escribe tu nombre de usuario para confirmar:
                <strong>{deleteConfirmationTarget}</strong>
              </p>
              <label className="settings-delete-confirm-field">
                <span>Nombre de usuario</span>
                <input
                  autoFocus
                  value={deleteConfirmationValue}
                  onChange={(event) => setDeleteConfirmationValue(event.target.value)}
                  placeholder={deleteConfirmationTarget}
                />
              </label>
            </div>
          </ModalBody>
          <ModalFooter align="end">
            <button className="secondary-button" type="button" onClick={closeDeleteModal} disabled={deleteSaving}>
              Cancelar
            </button>
            <button className="danger-button" type="button" onClick={handleDeleteAccount} disabled={deleteSaving || !deleteConfirmationMatches}>
              <AlertTriangle size={15} />
              {deleteSaving ? 'Eliminando...' : 'Eliminar cuenta'}
            </button>
          </ModalFooter>
        </ModalDialog>
      )}
    </div>
  );
}
