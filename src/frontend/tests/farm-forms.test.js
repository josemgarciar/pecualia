import assert from 'node:assert/strict';
import { test } from 'node:test';
import {
  buildBookPdfPath, buildConsecutiveIdentificationPreview, createAnimalDetailForm,
  createAutorrepositionForm, createDeathFormState, createFarmSettingsForm,
  createManualOvineCaprineAnimalForm, createManualPorcineAnimalForm,
  emptyToNull, parseOptionalNumber, validateAnimalDetailForm, validateAutorrepositionForm,
  validateFarmSettingsForm, validateManualOvineCaprineAnimalForm, validateManualPorcineAnimalForm
} from '../src/features/farms/farmForms.js';
import { formatDeathDestination, getDeathDestinationOptions } from '../src/features/farms/farmFormatting.js';

const officialIdentification = 'ES123456789012';

test('consecutive animal identifiers preserve zero padding and numeric precision', () => {
  assert.deepEqual(buildConsecutiveIdentificationPreview(' es000000000001 ', '3'), {
    firstIdentification: 'ES000000000001', lastIdentification: 'ES000000000003', count: 3
  });
  assert.equal(buildConsecutiveIdentificationPreview('GT9007199254740993', '2').lastIdentification, 'GT9007199254740994');
});

test('consecutive identifiers reject invalid counts, nonnumeric suffixes and overflow', () => {
  for (const count of ['0', '-1', '1.5', 'invalid']) {
    assert.equal(buildConsecutiveIdentificationPreview(officialIdentification, count), null);
  }
  assert.equal(buildConsecutiveIdentificationPreview('', '1'), null);
  assert.throws(() => buildConsecutiveIdentificationPreview('ESABC', '1'), /secuencia numérica/);
  assert.throws(() => buildConsecutiveIdentificationPreview('GT999', '2'), /desborda/);
});

test('autorreposition enforces census and age eligibility before submitting', () => {
  const form = { ...createAutorrepositionForm({ livestockSpecies: 'Ovine' }),
    startIdentification: officialIdentification, quantity: '3', breed: 'Merina', sex: 'Female' };
  assert.deepEqual(validateAutorrepositionForm(form, 'Ovine', { availableAnimals: 3, eligibleAnimals: 3 }), {});
  assert.match(validateAutorrepositionForm(form, 'Ovine', { availableAnimals: 2, eligibleAnimals: 3 }).quantity, /censo/);
  assert.match(validateAutorrepositionForm(form, 'Ovine', { availableAnimals: 3, eligibleAnimals: 2 }).quantity, /4 meses/);
  assert.equal(validateAutorrepositionForm(form, 'Porcine', { availableAnimals: 3, eligibleAnimals: 3 }).animalType, 'Campo obligatorio para porcino');
});

test('manual porcine animals and detail edits share identity and year validation', () => {
  const form = { ...createManualPorcineAnimalForm(), identification: 'GT123', animalType: 'Cebo' };
  assert.deepEqual(validateManualPorcineAnimalForm(form), {});
  for (const birthYear of ['1899', '2101', '2000.5', 'wrong']) {
    assert.equal(validateManualPorcineAnimalForm({ ...form, birthYear }).birthYear, 'Debe ser un año válido');
  }
  for (const birthYear of ['', '1900', '2100']) {
    assert.deepEqual(validateAnimalDetailForm({ ...form, birthYear }, 'Porcine'), {});
  }
  assert.equal(validateManualPorcineAnimalForm({ ...form, identification: '' }).identification, 'Campo obligatorio');
  assert.equal(validateManualPorcineAnimalForm({ ...form, originCode: 'invalid' }).originCode, 'Código REGA inválido');
});

test('ovine and caprine manual animals retain species-specific identification rules', () => {
  const form = { ...createManualOvineCaprineAnimalForm(), identification: officialIdentification };
  for (const species of ['Ovine', 'Caprine']) {
    assert.deepEqual(validateManualOvineCaprineAnimalForm(form, species), {});
    assert.deepEqual(validateManualOvineCaprineAnimalForm({ ...form, identification: `${officialIdentification}-ABC` }, species), {});
    assert.match(validateManualOvineCaprineAnimalForm({ ...form, identification: 'GT123' }, species).identification, /Formato inválido/);
  }
});

test('animal detail mapping preserves both species-specific nested fields and missing values', () => {
  const form = createAnimalDetailForm({ identification: officialIdentification, birthYear: 2024,
    registrationCauseValue: 'Entrada', ovinoCaprino: { genotyping: 'ARR', identificationDate: '2024-01-01' },
    porcino: { animalType: 'Cebo', identificationDate: '2024-02-01' } });
  assert.equal(form.birthYear, '2024');
  assert.equal(form.registrationCause, 'Entrada');
  assert.equal(form.genotyping, 'ARR');
  assert.equal(form.ovinoIdentificationDate, '2024-01-01');
  assert.equal(form.identificationDate, '2024-02-01');
  assert.equal(form.originCode, '');
});

test('farm settings keep porcine requirements and allow zero capacity', () => {
  const form = createFarmSettingsForm({ name: 'Finca', regaCode: officialIdentification,
    regime: 'Extensive', town: 'Madrid', province: 'Madrid', porcineMothersCapacity: 0 });
  assert.equal(form.porcineMothersCapacity, '0');
  assert.deepEqual(validateFarmSettingsForm(form, 'Ovine'), {});
  assert.equal(validateFarmSettingsForm(form, 'Porcine').porcineRegistryNumber, 'Campo obligatorio para porcino');
  assert.deepEqual(validateFarmSettingsForm({ ...form, porcineRegistryNumber: 'R1' }, 'Porcine'), {});
  assert.equal(validateFarmSettingsForm({ ...form, spindle: '0' }, 'Ovine').spindle, 'Debe ser un número entero válido');
  assert.equal(validateFarmSettingsForm({ ...form, porcineMothersCapacity: '-1' }, 'Porcine').porcineMothersCapacity, 'Debe ser un número entero válido');
});

test('MER-only death destinations stay restricted by species', () => {
  for (const species of ['Porcine', 'Caprine']) {
    assert.deepEqual(getDeathDestinationOptions(species), [{ value: 'MER', label: 'MER' }]);
    assert.equal(createDeathFormState(species).destinationCode, 'MER');
  }
  assert.equal(getDeathDestinationOptions('Ovine').length, 2);
  assert.equal(createDeathFormState('Ovine').destinationCode, '');
  assert.equal(formatDeathDestination('AR26-1234567'), 'MER · AR26-1234567');
});

test('optional input conversion preserves zero and leaves validation to callers', () => {
  assert.equal(parseOptionalNumber(''), null);
  assert.equal(parseOptionalNumber('0'), 0);
  assert.equal(parseOptionalNumber('-1'), -1);
  assert.equal(parseOptionalNumber('1.5'), 1.5);
  assert.ok(Number.isNaN(parseOptionalNumber('invalid')));
  assert.equal(emptyToNull('  '), null);
  assert.equal(emptyToNull(' value '), 'value');
});

test('book export retains repeated encoded section IDs and an empty selection', () => {
  assert.equal(buildBookPdfPath('farm-1', []), '/api/farms/farm-1/book/pdf');
  assert.equal(buildBookPdfPath('farm-1', ['animals', 'births & deaths']), '/api/farms/farm-1/book/pdf?sectionIds=animals&sectionIds=births+%26+deaths');
});
