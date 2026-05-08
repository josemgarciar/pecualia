const managerPlans = [
  {
    key: 'free',
    backendPlanType: 'Basic',
    audience: 'Manager',
    name: 'Free',
    price: 0,
    cadence: '/mes',
    summary: 'Para gestores que empiezan con una cartera reducida.',
    limits: [
      'Hasta 2 explotaciones',
      'Hasta 1 Ganader@'
    ]
  },
  {
    key: 'pro',
    backendPlanType: 'Professional',
    audience: 'Manager',
    name: 'Pro',
    price: 25,
    cadence: '/mes',
    summary: 'Para gestores con operativa estable y varios clientes activos.',
    limits: [
      'Hasta 20 explotaciones',
      'Hasta 18 ganaderos'
    ],
    recommended: true
  },
  {
    key: 'max',
    backendPlanType: 'Enterprise',
    audience: 'Manager',
    name: 'Max',
    price: 50,
    cadence: '/mes',
    summary: 'Para despachos y equipos que necesitan capacidad sin límites.',
    limits: [
      'Explotaciones ilimitadas',
      'Ganaderos ilimitados'
    ]
  }
];

const farmerPlans = [
  {
    key: 'free',
    backendPlanType: 'Basic',
    audience: 'Farmer',
    name: 'Free',
    price: 0,
    cadence: '/mes',
    summary: 'Para ganaderos con una estructura básica.',
    limits: [
      'Hasta 2 explotaciones'
    ]
  },
  {
    key: 'pro',
    backendPlanType: 'Professional',
    audience: 'Farmer',
    name: 'Pro',
    price: 20,
    cadence: '/mes',
    summary: 'Para ganaderos que superan el límite del plan gratuito.',
    limits: [
      'Más de 2 explotaciones'
    ],
    recommended: true
  }
];

function mapManagerPlanTypeToKey(planType) {
  switch (planType) {
    case 'Professional':
      return 'pro';
    case 'Enterprise':
      return 'max';
    case 'Basic':
    default:
      return 'free';
  }
}

function mapFarmerPlanTypeToKey(planType) {
  switch (planType) {
    case 'Professional':
    case 'Enterprise':
      return 'pro';
    case 'Basic':
    default:
      return 'free';
  }
}

export function getPlansForRole(role) {
  return role === 'Manager' ? managerPlans : farmerPlans;
}

export function getCurrentPlanKey(user) {
  if (user?.role === 'Manager') {
    return mapManagerPlanTypeToKey(user?.planType);
  }

  return mapFarmerPlanTypeToKey(user?.planType);
}

export function getCurrentPlan(user) {
  const plans = getPlansForRole(user?.role);
  const currentPlanKey = getCurrentPlanKey(user);
  return plans.find((plan) => plan.key === currentPlanKey) ?? plans[0];
}

export function getPlanLabel(user) {
  return getCurrentPlan(user).name;
}

export function getPlanAudienceLabel(role) {
  return role === 'Manager' ? 'gestión profesional' : 'operativa de explotación';
}
