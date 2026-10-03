import { isMerOnlyDeathSpecies } from './farmFormatting.js';
import {
  getAnimalIdentificationFormatMessage,
  isValidAnimalIdentification,
  isValidRegaCode
} from '../../shared/validation/identifiers.js';

export function createDeathFormState(species) {
  return {
    identification: '',
    animalType: '',
    quantity: '1',
    dischargeDate: new Date().toISOString().slice(0, 10),
    destinationCode: isMerOnlyDeathSpecies(species) ? 'MER' : '',
    merCode: ''
  };
}

export function createAutorrepositionForm(farm) {
  const today = new Date().toISOString().slice(0, 10);

  return {
    startIdentification: '',
    quantity: '1',
    breed: '',
    sex: '',
    registrationDate: today,
    genotyping: '',
    dominantAllele: '',
    lowAllele: '',
    animalType: '',
    identificationDate: farm?.livestockSpecies === 'Porcine' ? today : '',
    pigRegistrationNumber: '',
    tag: ''
  };
}

export function buildConsecutiveIdentificationPreview(startIdentification, numberOfAnimals) {
  const normalizedIdentification = startIdentification.trim().toUpperCase();
  const count = Number(numberOfAnimals);

  if (!normalizedIdentification || !Number.isInteger(count) || count <= 0) {
    return null;
  }

  let numericStartIndex = normalizedIdentification.length;
  while (numericStartIndex > 0 && /\d/.test(normalizedIdentification[numericStartIndex - 1])) {
    numericStartIndex -= 1;
  }

  if (numericStartIndex === normalizedIdentification.length) {
    throw new Error('La identificación inicial debe terminar en una secuencia numérica.');
  }

  const prefix = normalizedIdentification.slice(0, numericStartIndex);
  const numericPart = normalizedIdentification.slice(numericStartIndex);
  const initialNumber = BigInt(numericPart);
  const width = numericPart.length;
  const lastNumber = initialNumber + BigInt(count - 1);
  const lastDigits = lastNumber.toString().padStart(width, '0');

  if (lastDigits.length > width) {
    throw new Error('El rango solicitado desborda la longitud numérica de la identificación inicial.');
  }

  return {
    firstIdentification: `${prefix}${initialNumber.toString().padStart(width, '0')}`,
    lastIdentification: `${prefix}${lastDigits}`,
    count
  };
}

export function validateAutorrepositionForm(form, species, availability) {
  const errors = {};
  const count = Number(form.quantity);
  const totalAvailable = availability?.availableAnimals ?? 0;
  const totalEligible = availability?.eligibleAnimals ?? 0;

  if (!form.startIdentification.trim()) {
    errors.startIdentification = 'Campo obligatorio';
  } else if (!isValidAnimalIdentification(species, form.startIdentification)) {
    errors.startIdentification = getAnimalIdentificationFormatMessage(species);
  }

  if (!Number.isInteger(count) || count <= 0) {
    errors.quantity = 'Debe ser un número entero mayor que cero';
  }

  if (count > totalAvailable) {
    errors.quantity = 'No puedes autoreponer más animales que los no identificados disponibles en el censo';
  }

  if (count > totalEligible) {
    errors.quantity = 'Solo puedes autoreponer animales con más de 4 meses cumplidos';
  }

  if (!form.breed.trim()) {
    errors.breed = 'Campo obligatorio';
  }

  if (!form.sex.trim()) {
    errors.sex = 'Campo obligatorio';
  }

  if (!form.registrationDate) {
    errors.registrationDate = 'Campo obligatorio';
  }

  if (species === 'Porcine' && !form.animalType.trim()) {
    errors.animalType = 'Campo obligatorio para porcino';
  }

  if (Object.keys(errors).length > 0) {
    return errors;
  }

  try {
    buildConsecutiveIdentificationPreview(form.startIdentification, form.quantity);
  } catch (error) {
    errors.startIdentification = error.message;
  }

  return errors;
}

export function createAnimalDetailForm(animal) {
  return {
    identification: animal?.identification ?? '',
    birthYear: animal?.birthYear != null ? String(animal.birthYear) : '',
    birthDate: animal?.birthDate ?? '',
    breed: animal?.breed ?? '',
    sex: animal?.sex ?? '',
    registrationDate: animal?.registrationDate ?? '',
    registrationCause: animal?.registrationCauseValue ?? '',
    originCode: animal?.originCode ?? '',
    genotyping: animal?.ovinoCaprino?.genotyping ?? '',
    dominantAllele: animal?.ovinoCaprino?.dominantAllele ?? '',
    lowAllele: animal?.ovinoCaprino?.lowAllele ?? '',
    ovinoIdentificationDate: animal?.ovinoCaprino?.identificationDate ?? '',
    animalType: animal?.porcino?.animalType ?? '',
    identificationDate: animal?.porcino?.identificationDate ?? '',
    pigRegistrationNumber: animal?.porcino?.pigRegistrationNumber ?? '',
    tag: animal?.porcino?.tag ?? ''
  };
}

export function createManualPorcineAnimalForm() {
  const today = new Date().toISOString().slice(0, 10);

  return {
    identification: '',
    birthYear: '',
    breed: '',
    sex: '',
    registrationDate: today,
    registrationCause: 'Entrada',
    originCode: '',
    animalType: '',
    identificationDate: today,
    pigRegistrationNumber: '',
    tag: ''
  };
}

export function createManualOvineCaprineAnimalForm() {
  const today = new Date().toISOString().slice(0, 10);

  return {
    identification: '',
    birthDate: '',
    breed: '',
    sex: '',
    registrationDate: today,
    originCode: '',
    identificationDate: today,
    genotyping: '',
    dominantAllele: '',
    lowAllele: ''
  };
}

export function validateManualPorcineAnimalForm(form) {
  return validateAnimalDetailForm(form, 'Porcine');
}

function validateAnimalIdentity(form, species) {
  const errors = {};

  if (!form.identification.trim()) {
    errors.identification = 'Campo obligatorio';
  } else if (!isValidAnimalIdentification(species, form.identification)) {
    errors.identification = getAnimalIdentificationFormatMessage(species);
  }

  if (form.originCode.trim() && !isValidRegaCode(form.originCode)) {
    errors.originCode = 'Código REGA inválido';
  }

  return errors;
}

export function validateManualOvineCaprineAnimalForm(form, species) {
  return validateAnimalIdentity(form, species);
}

export function validateAnimalDetailForm(form, species) {
  const errors = validateAnimalIdentity(form, species);

  if (species === 'Porcine' && !form.animalType.trim()) {
    errors.animalType = 'Campo obligatorio para porcino';
  }

  if (form.birthYear !== '') {
    const birthYear = Number(form.birthYear);
    if (!Number.isInteger(birthYear) || birthYear < 1900 || birthYear > 2100) {
      errors.birthYear = 'Debe ser un año válido';
    }
  }

  return errors;
}

export function createFarmSettingsForm(farm) {
  return {
    name: farm?.name ?? '',
    regaCode: farm?.regaCode ?? '',
    regime: farm?.regime ?? '',
    town: farm?.town ?? '',
    province: farm?.province ?? '',
    address: farm?.address ?? '',
    zipCode: farm?.zipCode ?? '',
    porcineRegistryNumber: farm?.porcineRegistryNumber ?? '',
    livestockType: farm?.livestockType ?? '',
    porcineMothersCapacity: farm?.porcineMothersCapacity != null ? String(farm.porcineMothersCapacity) : '',
    porcineFatteningCapacity: farm?.porcineFatteningCapacity != null ? String(farm.porcineFatteningCapacity) : '',
    responsible: farm?.responsible ?? '',
    zootechnicClassification: farm?.zootechnicClassification ?? '',
    spindle: farm?.spindle != null ? String(farm.spindle) : '',
    xCoordinate: farm?.xCoordinate != null ? String(farm.xCoordinate) : '',
    yCoordinate: farm?.yCoordinate != null ? String(farm.yCoordinate) : ''
  };
}

export function validateFarmSettingsForm(form, species) {
  const errors = {};

  if (!form.name.trim()) {
    errors.name = 'Campo obligatorio';
  }
  if (!form.regaCode.trim()) {
    errors.regaCode = 'Campo obligatorio';
  } else if (!isValidRegaCode(form.regaCode)) {
    errors.regaCode = 'Formato REGA inválido (ej: ES061230000145)';
  }
  if (!form.regime) {
    errors.regime = 'Selecciona un régimen';
  }
  if (!form.town.trim()) {
    errors.town = 'Campo obligatorio';
  }
  if (!form.province) {
    errors.province = 'Selecciona una provincia';
  }
  if (species === 'Porcine' && !form.porcineRegistryNumber.trim()) {
    errors.porcineRegistryNumber = 'Campo obligatorio para porcino';
  }
  if (species === 'Porcine' && form.porcineMothersCapacity !== '') {
    const porcineMothersCapacity = Number(form.porcineMothersCapacity);
    if (!Number.isInteger(porcineMothersCapacity) || porcineMothersCapacity < 0) {
      errors.porcineMothersCapacity = 'Debe ser un número entero válido';
    }
  }
  if (species === 'Porcine' && form.porcineFatteningCapacity !== '') {
    const porcineFatteningCapacity = Number(form.porcineFatteningCapacity);
    if (!Number.isInteger(porcineFatteningCapacity) || porcineFatteningCapacity < 0) {
      errors.porcineFatteningCapacity = 'Debe ser un número entero válido';
    }
  }
  if (form.spindle !== '') {
    const spindle = Number(form.spindle);
    if (!Number.isInteger(spindle) || spindle < 1) {
      errors.spindle = 'Debe ser un número entero válido';
    }
  }
  if (form.xCoordinate !== '' && Number.isNaN(Number(form.xCoordinate))) {
    errors.xCoordinate = 'Debe ser un número válido';
  }
  if (form.yCoordinate !== '' && Number.isNaN(Number(form.yCoordinate))) {
    errors.yCoordinate = 'Debe ser un número válido';
  }

  return errors;
}

export function buildBookPdfPath(farmId, sectionIds) {
  const params = new URLSearchParams();
  sectionIds.forEach((sectionId) => params.append('sectionIds', sectionId));
  const query = params.toString();
  return `/api/farms/${farmId}/book/pdf${query ? `?${query}` : ''}`;
}

export function createVaccinationFormState() {
  return {
    animalIdentification: '',
    vaccinationDate: new Date().toISOString().slice(0, 10),
    nextDose: '',
    vaccinationType: '',
    observations: ''
  };
}

export function parseOptionalNumber(value) {
  return value === '' ? null : Number(value);
}

export function emptyToNull(value) {
  const normalized = value?.trim();
  return normalized ? normalized : null;
}
