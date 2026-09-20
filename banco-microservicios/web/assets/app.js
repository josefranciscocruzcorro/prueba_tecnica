/**
 * Arranque y enrutado de la consola.
 *
 * Es una aplicación de una sola página sin dependencias ni paso de compilación: módulos ES
 * nativos servidos tal cual por nginx. Para una consola de este tamaño, un framework añadiría
 * más superficie de mantenimiento que valor.
 */

import { apiClientes, ErrorApi, sesion } from './api.js';
import { avisoError, avisoExito, esc, html } from './ui.js';
import { vistaClientes, vistaCuentas, vistaMovimientos, vistaReportes, vistaResumen } from './vistas.js';

const RUTAS = {
  resumen: { titulo: 'Resumen', subtitulo: 'Estado general de sus cuentas y actividad reciente.', vista: vistaResumen },
  clientes: { titulo: 'Clientes', subtitulo: 'Maestro de personas y clientes del banco.', vista: vistaClientes },
  cuentas: { titulo: 'Cuentas', subtitulo: 'Cuentas de ahorros y corrientes con su saldo disponible.', vista: vistaCuentas },
  movimientos: { titulo: 'Movimientos', subtitulo: 'Depósitos y retiros registrados sobre las cuentas.', vista: vistaMovimientos },
  reportes: { titulo: 'Reportes', subtitulo: 'Estado de cuenta por cliente y rango de fechas.', vista: vistaReportes },
};

const RUTA_POR_DEFECTO = 'resumen';

/** Credenciales sembradas por el servicio de Clientes, según los casos de uso del enunciado. */
const USUARIOS_DEMO = [
  { clienteId: 'CLI-001', nombre: 'Jose Lema', contrasena: '1234' },
  { clienteId: 'CLI-002', nombre: 'Marianela Montalvo', contrasena: '5678' },
  { clienteId: 'CLI-003', nombre: 'Juan Osorio', contrasena: '1245' },
];

const pantallaAcceso = document.getElementById('pantalla-acceso');
const aplicacion = document.getElementById('aplicacion');
const contenedorVista = document.getElementById('vista');

/* ── Acceso ─────────────────────────────────────────────────────────────────────────────────── */

function pintarUsuariosDemo() {
  const lista = document.getElementById('acceso-demo');

  lista.innerHTML = USUARIOS_DEMO.map((usuario) => html`
    <button type="button" class="demo-usuario" data-cliente="${usuario.clienteId}" data-clave="${usuario.contrasena}">
      <span>${usuario.nombre}</span>
      <span>${usuario.clienteId} · ${usuario.contrasena}</span>
    </button>`).join('');

  lista.querySelectorAll('[data-cliente]').forEach((boton) =>
    boton.addEventListener('click', () => {
      document.getElementById('acceso-cliente').value = boton.dataset.cliente;
      document.getElementById('acceso-contrasena').value = boton.dataset.clave;
      document.getElementById('forma-acceso').requestSubmit();
    }));
}

function conectarAcceso() {
  const forma = document.getElementById('forma-acceso');
  const error = document.getElementById('acceso-error');
  const boton = forma.querySelector('[data-envio]');

  forma.addEventListener('submit', async (evento) => {
    evento.preventDefault();
    error.hidden = true;

    const datos = Object.fromEntries(new FormData(forma).entries());
    boton.disabled = true;
    boton.textContent = 'Entrando…';

    try {
      const nueva = await apiClientes.autenticar(datos.clienteId.trim(), datos.contrasena);
      sesion.guardar(nueva);
      avisoExito(`Hola, ${nueva.cliente.nombre.split(' ')[0]}`, 'Sesión iniciada correctamente.');
      await entrarEnLaAplicacion(nueva);
    } catch (fallo) {
      error.textContent = fallo instanceof ErrorApi
        ? fallo.message
        : 'No se pudo iniciar sesión. Inténtelo de nuevo.';
      error.hidden = false;
      document.getElementById('acceso-contrasena').focus();
    } finally {
      boton.disabled = false;
      boton.textContent = 'Entrar';
    }
  });
}

/* ── Enrutador ──────────────────────────────────────────────────────────────────────────────── */

/** Descompone `#/ruta?a=b` en el nombre de la ruta y sus parámetros. */
function rutaActual() {
  const bruto = window.location.hash.replace(/^#\/?/, '') || RUTA_POR_DEFECTO;
  const [nombre, cadena = ''] = bruto.split('?');

  return {
    nombre: Object.hasOwn(RUTAS, nombre) ? nombre : RUTA_POR_DEFECTO,
    parametros: new URLSearchParams(cadena),
  };
}

function marcarNavegacion(nombre) {
  document.querySelectorAll('[data-nav]').forEach((enlace) => {
    if (enlace.dataset.nav === nombre) {
      enlace.setAttribute('aria-current', 'page');
    } else {
      enlace.removeAttribute('aria-current');
    }
  });
}

async function navegar() {
  const activa = sesion.leer();
  if (!activa) return mostrarAcceso();

  // Se reafirma la pantalla en cada navegación: un cambio de ruta puede llegar sin recarga
  // (el usuario pulsa «atrás», o se restaura una sesión existente), y en ese caso nadie más
  // habría sacado la aplicación de su estado oculto.
  mostrarAplicacion(activa);

  const { nombre, parametros } = rutaActual();
  const ruta = RUTAS[nombre];

  document.getElementById('titulo-vista').textContent = ruta.titulo;
  document.getElementById('subtitulo-vista').textContent = ruta.subtitulo;
  document.title = `${ruta.titulo} · Banco`;
  marcarNavegacion(nombre);

  contenedorVista.innerHTML = '';

  try {
    await ruta.vista(contenedorVista, { cliente: activa.cliente, parametros });
  } catch (error) {
    // Una sesión caducada a mitad de navegación devuelve al usuario al acceso sin dejarlo colgado.
    if (error instanceof ErrorApi && error.estado === 401) {
      avisoError(error);
      return mostrarAcceso();
    }

    contenedorVista.innerHTML = html`<div class="vacio">
      <p class="vacio__titulo">No se pudo mostrar esta sección</p>
      <p>${error.message}</p>
    </div>`;
    avisoError(error);
  }

  return undefined;
}

/* ── Cambio de pantalla ─────────────────────────────────────────────────────────────────────── */

function mostrarAcceso() {
  sesion.cerrar();
  aplicacion.hidden = true;
  pantallaAcceso.hidden = false;
  document.title = 'Entrar · Banco';
  document.getElementById('acceso-cliente')?.focus();
}

/** Deja visible la aplicación y actualiza la ficha de sesión de la cabecera. */
function mostrarAplicacion(activa) {
  pantallaAcceso.hidden = true;
  aplicacion.hidden = false;

  document.getElementById('sesion-nombre').textContent = activa.cliente.nombre;
  document.getElementById('sesion-id').textContent = activa.cliente.clienteId;
}

async function entrarEnLaAplicacion(activa) {
  mostrarAplicacion(activa);

  if (!window.location.hash) {
    window.location.hash = `#/${RUTA_POR_DEFECTO}`;
    return;
  }

  await navegar();
}

/* ── Arranque ───────────────────────────────────────────────────────────────────────────────── */

function conectarSalida() {
  document.getElementById('boton-salir').addEventListener('click', () => {
    mostrarAcceso();
    window.location.hash = '';
    avisoExito('Sesión cerrada', 'Hasta pronto.');
  });
}

async function iniciar() {
  pintarUsuariosDemo();
  conectarAcceso();
  conectarSalida();

  window.addEventListener('hashchange', navegar);

  const activa = sesion.leer();

  if (activa) {
    await entrarEnLaAplicacion(activa);
  } else {
    mostrarAcceso();
  }
}

// Cualquier fallo no previsto se muestra como aviso en vez de morir en silencio en la consola.
window.addEventListener('unhandledrejection', (evento) => {
  if (evento.reason instanceof ErrorApi) {
    avisoError(evento.reason);
    evento.preventDefault();
  }
});

iniciar().catch((error) => {
  document.body.innerHTML = `<p style="padding:32px">No se pudo iniciar la consola: ${esc(error.message)}</p>`;
});
