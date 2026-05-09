import { useEffect, useState } from 'react';
import { Pencil, RefreshCw, User, UserMinus } from 'lucide-react';
import { apiRequest } from '../../shared/api/client';
import { ModalBody, ModalDialog, ModalFooter, ModalHeader } from '../../shared/components/modal/Modal';

function formatStatus(status) {
  if (!status) {
    return '';
  }

  return status === 'PendingActivation' ? 'Pendiente' : 'Activo';
}

function formatPersonType(personType) {
  return personType === 'Company' ? 'Persona jurídica' : 'Persona física';
}

export function FarmerDetailPage({
  farmerId,
  token,
  onClose,
  onEdit,
  onOpenFarms,
  onSuccess,
  onError,
  onUnlinked
}) {
  const [farmer, setFarmer] = useState(null);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const statusLabel = farmer ? formatStatus(farmer.status) : '';

  const loadFarmerDetail = async () => {
    if (!farmerId) {
      setFarmer(null);
      return;
    }

    setLoading(true);
    try {
      const response = await apiRequest(`/api/farmers/${farmerId}`, { token });
      setFarmer(response);
    } catch (requestError) {
      onError(requestError.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadFarmerDetail();
  }, [farmerId, token]);

  const handleResend = async () => {
    if (!farmerId) {
      return;
    }

    setActionLoading(true);
    try {
      const response = await apiRequest(`/api/farmers/${farmerId}/send-activation`, {
        method: 'POST',
        token
      });

      onSuccess(response.resent ? 'Invitación reenviada correctamente.' : 'La cuenta ya está activa.');
      await loadFarmerDetail();
    } catch (requestError) {
      onError(requestError.message);
    } finally {
      setActionLoading(false);
    }
  };

  const handleUnlink = async () => {
    if (!farmerId || !farmer) {
      return;
    }

    const confirmed = window.confirm(`Se desvinculará a "${farmer.displayName}" de tu cartera de gestión. La cuenta y sus explotaciones seguirán existiendo. ¿Quieres continuar?`);
    if (!confirmed) {
      return;
    }

    setActionLoading(true);
    try {
      await apiRequest(`/api/farmers/${farmerId}/manager-link`, {
        method: 'DELETE',
        token
      });

      onSuccess('Ganader@ desvinculado correctamente del gestor.');
      await onUnlinked();
    } catch (requestError) {
      onError(requestError.message);
    } finally {
      setActionLoading(false);
    }
  };

  return (
    <ModalDialog size="wide" shellClassName="farmer-detail-modal">
      <ModalHeader
        icon={<User size={18} />}
        title="Ficha del Ganader@"
        subtitle={loading || !farmer ? 'Cargando información...' : formatPersonType(farmer.personType)}
        onClose={onClose}
      />
      <ModalBody className="farmer-detail-body">
          {loading ? (
            <div className="empty-state">Cargando ficha del Ganader@...</div>
          ) : !farmer ? (
            <div className="empty-state">No se pudo cargar el detalle del Ganader@.</div>
          ) : (
            <div className="stack farmer-detail-layout">
              <section className="farmer-detail-hero">
                <div className="confirmation-hero-icon farmer-detail-hero-icon">
                  <User size={20} />
                </div>
                <div className="farmer-detail-hero-copy">
                  <strong>{farmer.displayName}</strong>
                  <span>{formatPersonType(farmer.personType)}</span>
                  <span>{farmer.nifCif}</span>
                </div>
                {statusLabel ? <span className={`status-chip status-${farmer.status}`}>{statusLabel}</span> : null}
              </section>

              <div className="farmer-detail-summary-grid">
                <section className="farm-summary-card">
                  <div className="farm-summary-card-header">
                    <p>DATOS PRINCIPALES</p>
                  </div>
                  <div className="farmer-detail-card-body detail-grid">
                    <div><span>Email</span><strong>{farmer.email || 'No informado'}</strong></div>
                    <div><span>Teléfono</span><strong>{farmer.phoneNumber || 'No informado'}</strong></div>
                    <div><span>Provincia</span><strong>{farmer.province || 'No informada'}</strong></div>
                    <div><span>Localidad</span><strong>{farmer.town || 'No informada'}</strong></div>
                    <div><span>Código postal</span><strong>{farmer.zipCode || 'No informado'}</strong></div>
                    <div className="detail-full"><span>Dirección</span><strong>{farmer.residence || 'No informada'}</strong></div>
                  </div>
                </section>

                <section className="farm-summary-card">
                  <div className="farm-summary-card-header">
                    <p>{farmer.personType === 'Individual' ? 'IDENTIDAD PERSONAL' : 'DATOS SOCIETARIOS'}</p>
                  </div>
                  <div className="farmer-detail-card-body detail-grid">
                    {farmer.personType === 'Individual' ? (
                      <>
                        <div><span>Nombre</span><strong>{farmer.name || 'No informado'}</strong></div>
                        <div><span>Primer apellido</span><strong>{farmer.firstSurname || 'No informado'}</strong></div>
                        <div><span>Segundo apellido</span><strong>{farmer.secondSurname || 'No informado'}</strong></div>
                        <div><span>Nacimiento</span><strong>{farmer.birthDate || 'No informado'}</strong></div>
                      </>
                    ) : (
                      <>
                        <div><span>Razón social</span><strong>{farmer.companyName || 'No informada'}</strong></div>
                        <div className="detail-full"><span>Representante legal</span><strong>{farmer.legalRepresentative || 'No informado'}</strong></div>
                      </>
                    )}
                  </div>
                </section>
              </div>

              <section className="farm-summary-card">
                <div className="farm-summary-card-header">
                  <p>ACCIONES</p>
                </div>
                <div className="farmer-detail-actions">
                  <button className="secondary-button" type="button" onClick={() => onEdit(farmer)} disabled={actionLoading}>
                    <Pencil size={15} />
                    Editar
                  </button>
                  {farmer.canResendActivation && (
                    <button className="secondary-button" type="button" onClick={handleResend} disabled={actionLoading}>
                      <RefreshCw size={15} />
                      Reenviar invitación
                    </button>
                  )}
                </div>
                <div className="farmer-detail-danger-zone">
                  <button className="danger-button" type="button" onClick={handleUnlink} disabled={actionLoading}>
                    <UserMinus size={15} />
                    Desvincular del gestor
                  </button>
                </div>
              </section>

              <section className="farm-summary-card">
                <div className="farm-summary-card-header">
                  <p>EXPLOTACIONES ASOCIADAS</p>
                </div>
                <div className="farmer-detail-card-body">
                  {farmer.farms.length === 0 ? (
                    <div className="empty-state">Este Ganader@ todavía no tiene explotaciones registradas.</div>
                  ) : (
                    <div className="table-list">
                      {farmer.farms.map((farm) => (
                        <article className="list-row" key={farm.id}>
                          <div>
                            <strong>{farm.name}</strong>
                            <div className="muted-text">{farm.regaCode} · {farm.livestockSpecies}</div>
                          </div>
                          <div className="row-actions">
                            <span className={`status-chip status-${farm.status}`}>{farm.status}</span>
                            <span className="muted-text">{farm.animalCount} animales</span>
                          </div>
                        </article>
                      ))}
                    </div>
                  )}
                </div>
              </section>
            </div>
          )}
      </ModalBody>
      <ModalFooter align="end">
        <button className="primary-button" type="button" onClick={onClose}>Cerrar</button>
      </ModalFooter>
    </ModalDialog>
  );
}
