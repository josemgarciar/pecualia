import { CheckCircle2, X } from 'lucide-react';

function classNames(...values) {
  return values.filter(Boolean).join(' ');
}

export function ModalDialog({
  children,
  cardAs: CardComponent = 'div',
  size = 'default',
  shellClassName = '',
  backdropClassName = '',
  ...cardProps
}) {
  return (
    <div className={classNames('modal-backdrop', backdropClassName)} role="dialog" aria-modal="true">
      <CardComponent
        className={classNames(
          'modal-card',
          size === 'wide' && 'modal-wide',
          'farm-modal-shell',
          shellClassName
        )}
        {...cardProps}
      >
        {children}
      </CardComponent>
    </div>
  );
}

export function ModalHeader({
  icon,
  title,
  subtitle,
  onClose,
  closeLabel = 'Cerrar modal',
  actions
}) {
  return (
    <div className="farm-modal-header">
      <div className="farm-modal-title">
        {icon && <div className="modal-panel-icon">{icon}</div>}
        <div className="farm-modal-title-copy">
          <h2>{title}</h2>
          {subtitle && <p>{subtitle}</p>}
        </div>
      </div>
      {actions ?? (
        <button className="farm-modal-close" type="button" onClick={onClose} aria-label={closeLabel}>
          <X size={18} />
        </button>
      )}
    </div>
  );
}

export function ModalStepper({ steps, currentStep, className = '' }) {
  return (
    <div
      className={classNames('farm-stepper', className)}
      style={{ gridTemplateColumns: `repeat(${steps.length}, minmax(0, 1fr))` }}
    >
      {steps.map((item, index) => {
        const stepNumber = index + 1;
        const isDone = currentStep > stepNumber;
        const isActive = currentStep === stepNumber;
        const Icon = item.icon;

        return (
          <div className="farm-stepper-item" key={item.label}>
            <div className="farm-stepper-marker-group">
              <div
                className={classNames(
                  'farm-stepper-marker',
                  (isDone || isActive) && 'farm-stepper-marker-active',
                  isDone && 'farm-stepper-marker-done'
                )}
              >
                {isDone ? <CheckCircle2 size={16} /> : Icon ? <Icon size={15} /> : stepNumber}
              </div>
              {index < steps.length - 1 && (
                <div className={classNames('farm-stepper-connector', isDone && 'farm-stepper-connector-done')} />
              )}
            </div>
            <span className={classNames('farm-stepper-label', (isDone || isActive) && 'farm-stepper-label-active')}>
              {item.label}
            </span>
          </div>
        );
      })}
    </div>
  );
}

export function ModalBody({ children, className = '' }) {
  return <div className={classNames('farm-modal-body', className)}>{children}</div>;
}

export function ModalFooter({ children, align = 'between', className = '' }) {
  return (
    <div className={classNames('farm-modal-footer', `farm-modal-footer-${align}`, className)}>
      {children}
    </div>
  );
}
