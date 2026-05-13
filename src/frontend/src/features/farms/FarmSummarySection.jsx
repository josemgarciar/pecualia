import { DetailField, formatCoordinate, formatRegime, formatText, speciesToneMap } from './FarmDetailShared';

export function FarmSummarySection({ farm, summaryCensus }) {
  const speciesTone = speciesToneMap[farm.livestockSpecies] ?? { label: farm.livestockSpecies };

  return (
    <section className="farm-detail-grid">
      <article className="panel-card stack">
        <div className="detail-header">
          <div>
            <h2>Datos oficiales</h2>
            <p>Información principal de la explotación registrada en Pecualia.</p>
          </div>
        </div>
        <div className="detail-grid">
          <DetailField label="Titular" value={farm.farmerName} />
          <DetailField label="Especie" value={speciesTone.label} />
          <DetailField label="Régimen" value={formatRegime(farm.regime)} />
          <DetailField label="Tipo de explotación" value={formatText(farm.livestockType)} />
          {farm.livestockSpecies === 'Porcine' && (
            <DetailField label="Registro porcino" value={formatText(farm.porcineRegistryNumber)} />
          )}
          {farm.livestockSpecies === 'Porcine' && (
            <DetailField label="Capacidad máxima madres" value={formatText(farm.porcineMothersCapacity)} />
          )}
          {farm.livestockSpecies === 'Porcine' && (
            <DetailField label="Capacidad máxima cebo" value={formatText(farm.porcineFatteningCapacity)} />
          )}
          <DetailField label="Clasificación zootécnica" value={formatText(farm.zootechnicClassification)} />
        </div>
      </article>

      <article className="panel-card stack">
        <div className="detail-header">
          <div>
            <h2>Ubicación y responsable</h2>
            <p>Datos administrativos y coordenadas asociadas a la explotación.</p>
          </div>
        </div>
        <div className="detail-grid">
          <DetailField label="Provincia" value={formatText(farm.province)} />
          <DetailField label="Localidad" value={formatText(farm.town)} />
          <DetailField label="Código postal" value={formatText(farm.zipCode)} />
          <DetailField label="Huso" value={farm.spindle ?? 'No informado'} />
          <DetailField label="Coordenada X" value={formatCoordinate(farm.xCoordinate)} />
          <DetailField label="Coordenada Y" value={formatCoordinate(farm.yCoordinate)} />
          <DetailField label="Responsable" value={formatText(farm.responsible)} />
          <DetailField label="Dirección" value={formatText(farm.address)} fullWidth />
        </div>
      </article>
    </section>
  );
}
