import { useMemo, useState } from 'react';
import { ArrowRight, BadgeCheck, CircleAlert, CreditCard, ShieldCheck } from 'lucide-react';
import { useAuth } from '../../shared/auth/AuthContext';
import { getCurrentPlan, getPlansForRole } from '../../shared/subscription/plans';

function formatDate(value) {
  if (!value) {
    return 'Pendiente';
  }

  const [year, month, day] = String(value).split('-');
  if (!year || !month || !day) {
    return String(value);
  }

  return `${day}/${month}/${year}`;
}

function formatPrice(price) {
  return price === 0 ? '0€' : `${price}€`;
}

export function SubscriptionPage() {
  const { user } = useAuth();
  const [feedback, setFeedback] = useState('');
  const currentPlan = useMemo(() => getCurrentPlan(user), [user]);
  const plans = useMemo(() => getPlansForRole(user?.role), [user?.role]);
  const renewalDate = formatDate(user?.subscriptionExpirationDate);
  const startDate = formatDate(user?.subscriptionInitialDate);
  const hasPaidPlan = currentPlan.price > 0;
  const audienceTitle = user?.role === 'Manager' ? 'planes para gestores' : 'planes para ganaderos';
  const subscriptionState = user?.subscriptionState === 'Active' ? 'Activa' : 'Pendiente';
  const renewText = user?.subscriptionAutorenew
    ? 'Activada'
    : 'Pendiente de activar con Stripe';

  function handlePlanSelection(planName) {
    setFeedback(`La contratación del plan ${planName} se conectará con Stripe próximamente.`);
  }

  return (
    <div className="page-stack">
      <header className="page-header">
        <div>
          <h1>Suscripción</h1>
          <p>Configura {audienceTitle} con los límites y precios definitivos que se integrarán después con Stripe.</p>
        </div>
      </header>

      {feedback && <div className="success-banner">{feedback}</div>}

      <section className="subscription-current-card">
        <div className="subscription-current-header">
          <div className="subscription-current-title-group">
            <div className="subscription-current-icon">
              <CreditCard size={24} />
            </div>
            <div className="subscription-current-copy">
              <div className="subscription-current-title-row">
                <h2>Plan {currentPlan.name}</h2>
                <span className="subscription-status-pill">{subscriptionState}</span>
              </div>
              <p>{currentPlan.summary}</p>
            </div>
          </div>

          <div className="subscription-price-block">
            <strong>{formatPrice(currentPlan.price)}</strong>
            <span>{currentPlan.cadence}</span>
          </div>
        </div>

        <div className="subscription-current-meta">
          <div>
            <span>Fecha de inicio</span>
            <strong>{startDate}</strong>
          </div>
          <div>
            <span>Próxima revisión</span>
            <strong>{renewalDate}</strong>
          </div>
          <div>
            <span>Renovación automática</span>
            <strong>{renewText}</strong>
          </div>
          <div>
            <span>Modalidad</span>
            <strong>{user?.role === 'Manager' ? 'Gestor' : 'Ganadero'}</strong>
          </div>
        </div>

        <div className="subscription-current-actions">
          <button className="secondary-button" type="button" onClick={() => handlePlanSelection(currentPlan.name)}>
            <CreditCard size={16} />
            Gestionar facturación
          </button>
        </div>
      </section>

      <section className="subscription-renewal-banner">
        <CircleAlert size={24} />
        <div>
          <strong>{hasPaidPlan ? 'Estado del plan de pago' : 'Plan gratuito activo'}</strong>
          <p>
            {hasPaidPlan
              ? `Tu plan ${currentPlan.name} está preparado para conectarse con Stripe. La próxima fecha visible es ${renewalDate}.`
              : 'Tu cuenta está en el plan Free. Podrás ampliar capacidad cuando la contratación online quede conectada con Stripe.'}
          </p>
        </div>
      </section>

      <section className="subscription-plans-shell">
        <div className="subscription-section-heading">
          <h2>Comparativa de planes</h2>
          <p>Estos límites y precios sustituyen a los del mockup anterior y reflejan las modalidades reales definidas para la aplicación.</p>
        </div>

        <div className={`subscription-plan-grid${plans.length === 2 ? ' subscription-plan-grid-compact' : ''}`}>
          {plans.map((plan) => {
            const isCurrent = plan.key === currentPlan.key;

            return (
              <article
                key={plan.key}
                className={isCurrent ? 'subscription-plan-card subscription-plan-card-current' : 'subscription-plan-card'}
              >
                {plan.recommended && <div className="subscription-plan-ribbon">Recomendado</div>}

                <div className="subscription-plan-header">
                  <div>
                    <div className="subscription-plan-title-row">
                      <h3>{plan.name}</h3>
                      {isCurrent && <span className="subscription-current-pill">Actual</span>}
                    </div>
                    <div className="subscription-plan-price">
                      <strong>{formatPrice(plan.price)}</strong>
                      <span>{plan.cadence}</span>
                    </div>
                    <p>{plan.summary}</p>
                  </div>
                </div>

                <div className="subscription-plan-features">
                  {plan.limits.map((limit) => (
                    <div key={limit} className="subscription-plan-feature">
                      <BadgeCheck size={18} />
                      <span>{limit}</span>
                    </div>
                  ))}
                </div>

                <div className="subscription-plan-action">
                  {isCurrent ? (
                    <button className="subscription-current-button" type="button" disabled>
                      <ShieldCheck size={16} />
                      Plan actual
                    </button>
                  ) : (
                    <button className="subscription-select-button" type="button" onClick={() => handlePlanSelection(plan.name)}>
                      Cambiar a {plan.name}
                      <ArrowRight size={16} />
                    </button>
                  )}
                </div>
              </article>
            );
          })}
        </div>
      </section>
    </div>
  );
}
