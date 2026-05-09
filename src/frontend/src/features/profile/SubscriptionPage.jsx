import { useEffect, useMemo, useRef, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { ArrowRight, BadgeCheck, CircleAlert, CreditCard, ShieldCheck } from 'lucide-react';
import { apiRequest } from '../../shared/api/client';
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
  const location = useLocation();
  const navigate = useNavigate();
  const { token, user, refreshProfile } = useAuth();
  const [feedback, setFeedback] = useState('');
  const [error, setError] = useState('');
  const [loadingAction, setLoadingAction] = useState('');
  const handledCheckoutRef = useRef('');
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

  useEffect(() => {
    const params = new URLSearchParams(location.search);
    const checkoutState = params.get('checkout');
    const sessionId = params.get('session_id');
    const handledKey = checkoutState === 'success' && sessionId
      ? `success:${sessionId}`
      : checkoutState === 'cancelled'
        ? 'cancelled'
        : '';

    if (checkoutState === 'cancelled') {
      if (handledCheckoutRef.current === handledKey) {
        return;
      }

      handledCheckoutRef.current = handledKey;
      setError('');
      setFeedback('La contratación se ha cancelado antes de completar el pago.');
      navigate(location.pathname, { replace: true });
      return;
    }

    if (checkoutState !== 'success' || !sessionId || !token) {
      return;
    }

    if (handledCheckoutRef.current === handledKey) {
      return;
    }

    handledCheckoutRef.current = handledKey;

    let cancelled = false;
    setError('');
    setFeedback('Pago confirmado en Stripe. Sincronizando la suscripción...');
    setLoadingAction('sync');

    apiRequest(`/api/billing/checkout-session-status/${encodeURIComponent(sessionId)}`, { token })
      .then(async () => {
        await refreshProfile();
        if (!cancelled) {
          setFeedback('La suscripción se ha conectado correctamente con Stripe.');
          navigate(location.pathname, { replace: true });
        }
      })
      .catch((requestError) => {
        if (!cancelled) {
          setError(requestError.message);
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoadingAction('');
        }
      });

    return () => {
      cancelled = true;
    };
  }, [location.pathname, location.search, navigate, refreshProfile, token]);

  async function handlePortalSession() {
    if (!token) {
      return;
    }

    setLoadingAction('portal');
    setError('');
    setFeedback('');

    try {
      const response = await apiRequest('/api/billing/portal-session', {
        method: 'POST',
        token
      });

      window.location.assign(response.portalUrl);
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setLoadingAction('');
    }
  }

  async function handlePlanSelection(plan) {
    if (!token) {
      return;
    }

    if (plan.price === 0) {
      await handlePortalSession();
      return;
    }

    setLoadingAction(plan.key);
    setError('');
    setFeedback('');

    try {
      const response = await apiRequest('/api/billing/checkout-session', {
        method: 'POST',
        token,
        body: {
          planType: plan.backendPlanType
        }
      });

      window.location.assign(response.checkoutUrl);
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setLoadingAction('');
    }
  }

  return (
    <div className="page-stack">
      <header className="page-header">
        <div>
          <h1>Suscripción</h1>
          <p>Configura {audienceTitle} con los límites y precios reales conectados con Stripe.</p>
        </div>
      </header>

      {error && <div className="error-banner">{error}</div>}
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
            <strong>{user?.role === 'Manager' ? 'Gestor' : 'Ganader@'}</strong>
          </div>
        </div>

        <div className="subscription-current-actions">
          <button
            className="secondary-button"
            type="button"
            onClick={hasPaidPlan ? handlePortalSession : undefined}
            disabled={!hasPaidPlan || loadingAction === 'portal' || loadingAction === 'sync'}
          >
            <CreditCard size={16} />
            {hasPaidPlan ? 'Gestionar facturación' : 'Disponible al contratar un plan de pago'}
          </button>
        </div>
      </section>

      <section className="subscription-renewal-banner">
        <CircleAlert size={24} />
        <div>
          <strong>{hasPaidPlan ? 'Estado del plan de pago' : 'Plan gratuito activo'}</strong>
          <p>
            {hasPaidPlan
              ? `Tu plan ${currentPlan.name} ya se gestiona con Stripe. La próxima fecha visible es ${renewalDate}.`
              : 'Tu cuenta está en el plan Free. Puedes ampliar capacidad iniciando la contratación online con Stripe.'}
          </p>
        </div>
      </section>

      <section className="subscription-plans-shell">
        <div className="subscription-section-heading">
          <h2>Comparativa de planes</h2>
          <p>Los planes de pago se contratan con Stripe. Las bajas y cambios desde un plan ya activo se gestionan desde el portal de facturación.</p>
        </div>

        <div className={`subscription-plan-grid${plans.length === 2 ? ' subscription-plan-grid-compact' : ''}`}>
          {plans.map((plan) => {
            const isCurrent = plan.key === currentPlan.key;
            const isBusy = loadingAction === plan.key || loadingAction === 'portal' || loadingAction === 'sync';

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
                    <button
                      className="subscription-select-button"
                      type="button"
                      disabled={isBusy}
                      onClick={() => handlePlanSelection(plan)}
                    >
                      {plan.price === 0 ? 'Gestionar baja o cambio' : `Cambiar a ${plan.name}`}
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
