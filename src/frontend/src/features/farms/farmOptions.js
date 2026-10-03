export const speciesToneMap = {
  Ovine: { bg: '#DDEBDF', color: '#2F6B4F', label: 'Ovino' },
  Caprine: { bg: '#DBEAFE', color: '#2563EB', label: 'Caprino' },
  Porcine: { bg: '#FCE7F3', color: '#9D174D', label: 'Porcino' }
};

export const regimeLabelMap = {
  Extensive: 'Extensivo',
  SemiExtensive: 'Semiextensivo',
  Intensive: 'Intensivo'
};

export const animalSexLabelMap = {
  Female: 'Hembra',
  Male: 'Macho'
};

export const animalRegistrationCauseLabelMap = {
  Entrada: 'Entrada (E)',
  Autorreposicion: 'Autorreposición (A)'
};

export const animalDischargeCauseLabelMap = {
  Salida: 'Salida (S)',
  Muerte: 'Muerte (M)'
};

export const currentYear = new Date().getFullYear();

export const monthLabels = ['Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun', 'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic'];

export const BOOK_PREVIEW_MAX_PAGES = 3;

export const BOOK_PREVIEW_DEBOUNCE_MS = 450;

export const BOOK_PREVIEW_TARGET_WIDTH = 760;

export const FARM_ANIMALS_SEARCH_DEBOUNCE_MS = 300;

export const FARM_ANIMALS_DEFAULT_PAGE_SIZE = 25;

export const FARM_ANIMALS_PAGE_SIZE_OPTIONS = [10, 25, 50];

export const porcineAnimalTypeOptions = [
  'Verracos',
  'Cerdas vida',
  'Hembras reposición',
  'Machos reposición',
  'Lechones',
  'Recría',
  'Cebo'
];

export const regimeOptions = [
  { value: 'Extensive', label: 'Extensivo' },
  { value: 'SemiExtensive', label: 'Semiextensivo' },
  { value: 'Intensive', label: 'Intensivo' }
];

export const provinceOptions = [
  'Álava', 'Albacete', 'Alicante', 'Almería', 'Asturias', 'Ávila', 'Badajoz', 'Barcelona', 'Burgos', 'Cáceres',
  'Cádiz', 'Cantabria', 'Castellón', 'Ciudad Real', 'Córdoba', 'Cuenca', 'Girona', 'Granada', 'Guadalajara',
  'Guipúzcoa', 'Huelva', 'Huesca', 'Islas Baleares', 'Jaén', 'La Coruña', 'La Rioja', 'Las Palmas', 'León',
  'Lleida', 'Lugo', 'Madrid', 'Málaga', 'Murcia', 'Navarra', 'Ourense', 'Palencia', 'Pontevedra', 'Salamanca',
  'Santa Cruz de Tenerife', 'Segovia', 'Sevilla', 'Soria', 'Tarragona', 'Teruel', 'Toledo', 'Valencia',
  'Valladolid', 'Vizcaya', 'Zamora', 'Zaragoza'
];
