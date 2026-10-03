import {
  ArrowLeftRight,
  BarChart3,
  BookOpen,
  Building2,
  ClipboardCheck,
  Shield,
  Skull,
  Sprout,
  Tag,
  TriangleAlert
} from 'lucide-react';

export const detailTabs = [
  { key: 'summary', label: 'Resumen', icon: Building2, enabled: true },
  { key: 'animals', label: 'Animales', icon: Tag, enabled: true },
  { key: 'movements', label: 'Movimientos', icon: ArrowLeftRight, enabled: true },
  { key: 'births', label: 'Nacimientos', icon: Sprout, enabled: true },
  { key: 'deaths', label: 'Muertes', icon: Skull, enabled: true },
  { key: 'vaccinations', label: 'Vacunación', icon: Shield, enabled: true },
  { key: 'balances', label: 'Censos y balances', icon: BarChart3, enabled: true },
  { key: 'book', label: 'Libro', icon: BookOpen, enabled: true },
  { key: 'incidents', label: 'Incidencias', icon: TriangleAlert, enabled: true },
  { key: 'inspections', label: 'Inspecciones', icon: ClipboardCheck, enabled: true }
];
