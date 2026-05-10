const REGA_CODE_REGEX = /^ES\d{12}$/;
const OFFICIAL_ANIMAL_IDENTIFICATION_REGEX = /^ES\d{12}$/;
const OVINE_OR_CAPRINE_LEGACY_IDENTIFICATION_REGEX = /^ES\d{12}-[A-Z0-9]{3,}$/;
const PORCINE_ALTERNATIVE_IDENTIFICATION_REGEX = /^GT\d+$/;
const DNI_REGEX = /^\d{8}[A-Z]$/;
const NIE_REGEX = /^[XYZ]\d{7}[A-Z]$/;
const SPECIAL_NIF_REGEX = /^[KLM]\d{7}[A-Z]$/;
const COMPANY_TAX_IDENTIFIER_REGEX = /^[ABCDEFGHJNPQRSUVW]\d{7}[0-9A-J]$/;
const DNI_LETTERS = 'TRWAGMYFPDXBNJZSQVHLCKE';
const CIF_CONTROL_LETTERS = 'JABCDEFGHI';

export function normalizeRegaCode(value) {
  return value.trim().toUpperCase();
}

export function isValidRegaCode(value) {
  return REGA_CODE_REGEX.test(normalizeRegaCode(value));
}

export function normalizeAnimalIdentification(value) {
  const token = value.trim().toUpperCase();

  const officialMatch = token.match(/^ES[\s._-]*((?:\d[\s._-]*){12})(?:-([A-Z0-9]{3,}))?$/);
  if (officialMatch) {
    const digits = officialMatch[1].replace(/\D/g, '');
    const suffix = officialMatch[2] ? `-${officialMatch[2]}` : '';
    return `ES${digits}${suffix}`;
  }

  const porcineAlternativeMatch = token.match(/^GT[\s._-]*(\d+)$/);
  if (porcineAlternativeMatch) {
    return `GT${porcineAlternativeMatch[1]}`;
  }

  return token;
}

export function isValidAnimalIdentification(species, value) {
  const normalizedValue = normalizeAnimalIdentification(value);
  return species === 'Porcine'
    ? OFFICIAL_ANIMAL_IDENTIFICATION_REGEX.test(normalizedValue) ||
      PORCINE_ALTERNATIVE_IDENTIFICATION_REGEX.test(normalizedValue)
    : OFFICIAL_ANIMAL_IDENTIFICATION_REGEX.test(normalizedValue) ||
      OVINE_OR_CAPRINE_LEGACY_IDENTIFICATION_REGEX.test(normalizedValue);
}

export function getAnimalIdentificationFormatMessage(species) {
  return species === 'Porcine'
    ? 'Formato inválido. Usa ES + 12 dígitos o GT + números.'
    : 'Formato inválido. Usa ES + 12 dígitos o ES + 12 dígitos con sufijo.';
}

export function normalizeTaxIdentifier(value) {
  return value.trim().toUpperCase();
}

export function isValidTaxIdentifier(personType, value) {
  const normalizedValue = normalizeTaxIdentifier(value);
  return personType === 'Company'
    ? isValidCompanyTaxIdentifier(normalizedValue)
    : isValidIndividualTaxIdentifier(normalizedValue);
}

function isValidIndividualTaxIdentifier(value) {
  if (DNI_REGEX.test(value)) {
    return hasExpectedControlLetter(value.slice(0, 8), value.slice(-1));
  }

  if (NIE_REGEX.test(value)) {
    const prefix = value[0] === 'X' ? '0' : value[0] === 'Y' ? '1' : '2';
    return hasExpectedControlLetter(`${prefix}${value.slice(1, 8)}`, value.slice(-1));
  }

  if (SPECIAL_NIF_REGEX.test(value)) {
    return hasExpectedControlLetter(`0${value.slice(1, 8)}`, value.slice(-1));
  }

  return false;
}

function isValidCompanyTaxIdentifier(value) {
  if (!COMPANY_TAX_IDENTIFIER_REGEX.test(value)) {
    return false;
  }

  const bodyDigits = value.slice(1, 8);
  let sum = 0;

  for (let index = 0; index < bodyDigits.length; index += 1) {
    const digit = Number(bodyDigits[index]);
    if (index % 2 === 0) {
      const doubled = digit * 2;
      sum += Math.floor(doubled / 10) + (doubled % 10);
    } else {
      sum += digit;
    }
  }

  const controlDigit = (10 - (sum % 10)) % 10;
  const expectedDigit = String(controlDigit);
  const expectedLetter = CIF_CONTROL_LETTERS[controlDigit];
  const controlCharacter = value.slice(-1);

  if ('ABEH'.includes(value[0])) {
    return controlCharacter === expectedDigit;
  }

  if ('KPQSNW'.includes(value[0])) {
    return controlCharacter === expectedLetter;
  }

  return controlCharacter === expectedDigit || controlCharacter === expectedLetter;
}

function hasExpectedControlLetter(numericPart, controlLetter) {
  return DNI_LETTERS[Number(numericPart) % DNI_LETTERS.length] === controlLetter;
}
