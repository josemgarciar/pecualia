import { Component } from 'react';
import { Link } from 'react-router-dom';

export class AppErrorBoundary extends Component {
  state = { hasError: false };

  static getDerivedStateFromError() {
    return { hasError: true };
  }

  render() {
    if (!this.state.hasError) {
      return this.props.children;
    }

    return (
      <section className="panel-card stack" role="alert">
        <h2>No se ha podido mostrar esta sección</h2>
        <p>Puedes intentarlo de nuevo o volver al inicio.</p>
        <button
          className="primary-button"
          type="button"
          onClick={() => this.setState({ hasError: false })}
        >
          Reintentar
        </button>
        <Link to="/app/dashboard">Volver al inicio</Link>
      </section>
    );
  }
}
