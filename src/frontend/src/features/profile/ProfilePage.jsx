import { useAuth } from '../../shared/auth/AuthContext';

export function ProfilePage() {
  const { user } = useAuth();

  return (
    <div className="page-stack">
      <header className="page-header">
        <div>
          <h1>Perfil</h1>
          <p>Datos de la cuenta autenticada y estado actual de acceso.</p>
        </div>
      </header>

      <section className="panel-card profile-grid">
        <div><span>Nombre</span><strong>{user?.name}</strong></div>
        <div><span>Apellidos</span><strong>{user?.surname}</strong></div>
        <div><span>Correo</span><strong>{user?.email}</strong></div>
        <div><span>Usuario</span><strong>{user?.username || 'Pendiente de activación'}</strong></div>
        <div><span>Rol</span><strong>{user?.role}</strong></div>
        <div><span>Estado</span><strong>{user?.isActive ? 'Activo' : 'Pendiente'}</strong></div>
        {user?.organizationName && <div><span>Organización</span><strong>{user.organizationName}</strong></div>}
        {user?.farmerStatus && <div><span>Estado ganadero</span><strong>{user.farmerStatus}</strong></div>}
      </section>
    </div>
  );
}
