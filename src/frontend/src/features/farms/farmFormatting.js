import {
  speciesToneMap,
  regimeLabelMap,
  animalSexLabelMap,
  animalRegistrationCauseLabelMap,
  animalDischargeCauseLabelMap
} from './farmOptions.js';
import { isMerDestinationCode } from '../../shared/validation/identifiers.js';

export function formatText(value, fallback = 'No informado') {
  return value ?? fallback;
}

export function formatLivestockSpecies(value, fallback = 'Sin especie') {
  return speciesToneMap[value]?.label ?? value ?? fallback;
}

export function formatRegime(value) {
  if (!value) {
    return 'No informado';
  }

  return regimeLabelMap[value] ?? value;
}

export function formatCoordinate(value) {
  if (value == null) {
    return 'No informada';
  }

  return new Intl.NumberFormat('es-ES', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2
  }).format(value);
}

export function formatDate(value) {
  return value ? new Intl.DateTimeFormat('es-ES').format(new Date(`${value}T00:00:00`)) : '—';
}

export function formatAnimalSex(value) {
  return animalSexLabelMap[value] ?? value ?? 'No informado';
}

export function formatAnimalCause(value) {
  return animalRegistrationCauseLabelMap[value] ?? animalDischargeCauseLabelMap[value] ?? value ?? 'No informada';
}

export function isMerOnlyDeathSpecies(species) {
  return species === 'Porcine' || species === 'Caprine';
}

export function getDeathDestinationOptions(species) {
  return isMerOnlyDeathSpecies(species)
    ? [{ value: 'MER', label: 'MER' }]
    : [
        { value: 'SANDACH', label: 'SANDACH' },
        { value: 'MER', label: 'MER' }
      ];
}

export function getDeathDestinationType(value) {
  if (isMerDestinationCode(value)) {
    return 'MER';
  }

  return value ?? '';
}

export function formatDeathDestination(value) {
  if (!value) {
    return '—';
  }

  return isMerDestinationCode(value) && value !== 'MER'
    ? `MER · ${value}`
    : value;
}

export function formatAnimalGuideSeries(entryGuideSerie, exitGuideSerie) {
  if (!entryGuideSerie && !exitGuideSerie) {
    return 'No informadas';
  }

  return `${entryGuideSerie ?? '—'} / ${exitGuideSerie ?? '—'}`;
}
