import { formatDate } from './farmFormatting.js';
import { DetailField } from './FarmDetailFields.jsx';
import { Edit3, Sprout, Trash2 } from 'lucide-react';
import { ModalBody, ModalDialog, ModalFooter, ModalHeader } from '../../shared/components/modal/Modal';

export function BirthDetailModal({ birth, farmName, onClose, onEdit, onDelete }) {
  if (!birth) {
    return null;
  }

  return (
    <ModalDialog size="wide">
      <ModalHeader
        icon={<Sprout size={18} />}
        title="Detalle del nacimiento"
        subtitle={farmName}
        onClose={onClose}
      />
      <ModalBody>
        <div className="profile-grid">
          <DetailField label="Fecha de parto" value={formatDate(birth.birthDate)} />
          <DetailField label="Crías declaradas" value={String(birth.offspringNumber)} />
          <DetailField label="Peso medio" value={birth.birthWeight == null ? 'No informado' : `${birth.birthWeight} kg`} />
          <DetailField label="Observaciones" value={birth.observations ?? 'No informadas'} fullWidth />
        </div>
      </ModalBody>
      <ModalFooter>
        <button className="danger-button" type="button" onClick={() => onDelete(birth)}>
          <Trash2 size={15} />
          Eliminar
        </button>
        <div className="animal-modal-actions">
          <button className="secondary-button" type="button" onClick={onClose}>Cerrar</button>
          <button className="primary-button" type="button" onClick={() => onEdit(birth)}>
            <Edit3 size={15} />
            Editar
          </button>
        </div>
      </ModalFooter>
    </ModalDialog>
  );
}
