/**
 * Piezas de interfaz reutilizables: formateo, plantillas seguras, avisos y diálogos.
 *
 * Todo el HTML se compone con la plantilla etiquetada `html`, que escapa cada interpolación.
 * Así ningún dato que venga del servidor —un nombre de cliente, el mensaje de un error— puede
 * inyectar marcado en la página, sin tener que acordarse de escapar caso por caso.
 */

const ESCAPES = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' };

/** Escapa un valor para insertarlo en HTML. */
export function esc(valor) {
  if (valor === null || valor === undefined) return '';
  return String(valor).replace(/[&<>"']/g, (caracter) => ESCAPES[caracter]);
}

/** Marca un fragmento como ya seguro, para componer plantillas anidadas. */
export function crudo(texto) {
  return { __html: String(texto ?? '') };
}

/** Plantilla etiquetada que escapa todo salvo lo marcado con `crudo`. */
export function html(literales, ...valores) {
  return literales.reduce((acumulado, literal, indice) => {
    if (indice === 0) return literal;

    const valor = valores[indice - 1];
    const texto = Array.isArray(valor)
      ? valor.map((v) => (v && v.__html !== undefined ? v.__html : esc(v))).join('')
      : valor && valor.__html !== undefined
        ? valor.__html
        : esc(valor);

    return acumulado + texto + literal;
  }, '');
}

const FORMATO_MONEDA = new Intl.NumberFormat('es-EC', {
  style: 'currency', currency: 'USD', minimumFractionDigits: 2, maximumFractionDigits: 2,
});

const FORMATO_FECHA = new Intl.DateTimeFormat('es-EC', { day: '2-digit', month: 'short', year: 'numeric' });

const FORMATO_FECHA_HORA = new Intl.DateTimeFormat('es-EC', {
  day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit',
});

export const formato = {
  dinero: (valor) => FORMATO_MONEDA.format(Number(valor ?? 0)),

  /** Importe con signo explícito: en un extracto, el signo es la información principal. */
  importe(valor) {
    const numero = Number(valor ?? 0);
    const signo = numero > 0 ? '+' : '';
    return `${signo}${FORMATO_MONEDA.format(numero)}`;
  },

  fecha: (valor) => (valor ? FORMATO_FECHA.format(new Date(valor)) : '—'),
  fechaHora: (valor) => (valor ? FORMATO_FECHA_HORA.format(new Date(valor)) : '—'),

  /** Fecha en formato aaaa-MM-dd, el que esperan los campos de tipo date y la API. */
  iso: (fecha) => new Date(fecha).toISOString().slice(0, 10),
};

/** Distintivo de estado activo/inactivo. */
export function distintivoEstado(activo) {
  return crudo(activo
    ? '<span class="distintivo distintivo--activo">Activo</span>'
    : '<span class="distintivo distintivo--inactivo">Inactivo</span>');
}

export function distintivo(texto, variante = '') {
  return crudo(`<span class="distintivo ${variante ? `distintivo--${esc(variante)}` : ''}">${esc(texto)}</span>`);
}

/** Importe coloreado según su signo. */
export function importeConColor(valor) {
  const numero = Number(valor ?? 0);
  const clase = numero >= 0 ? 'importe--positivo' : 'importe--negativo';
  return crudo(`<span class="${clase}">${esc(formato.importe(numero))}</span>`);
}

export function cargando() {
  return '<div class="cargando"><span class="girador" role="status" aria-label="Cargando"></span></div>';
}

export function vacio(titulo, detalle = '') {
  return html`<div class="vacio">
    <p class="vacio__titulo">${titulo}</p>
    ${detalle ? crudo(`<p>${esc(detalle)}</p>`) : ''}
  </div>`;
}

/* ── Avisos ─────────────────────────────────────────────────────────────────────────────────── */

const contenedorAvisos = () => document.getElementById('avisos');

/** Muestra un aviso efímero en la esquina inferior. */
export function avisar(titulo, { detalle = '', tipo = 'info', duracion = 4200 } = {}) {
  const elemento = document.createElement('div');
  elemento.className = `aviso aviso--${tipo}`;
  elemento.innerHTML = html`<div class="aviso__texto">
    <span class="aviso__titulo">${titulo}</span>
    ${detalle ? crudo(`<span class="aviso__detalle">${esc(detalle)}</span>`) : ''}
  </div>`;

  contenedorAvisos().append(elemento);
  setTimeout(() => elemento.remove(), duracion);
}

export const avisoExito = (titulo, detalle) => avisar(titulo, { detalle, tipo: 'exito' });
export const avisoInfo = (titulo, detalle) => avisar(titulo, { detalle, tipo: 'info' });

/**
 * Muestra un error de la API. Los fallos de validación se resumen en el propio aviso para que el
 * usuario sepa qué corregir sin tener que abrir la consola del navegador.
 */
export function avisoError(error) {
  const detalles = error?.detallesValidacion ?? [];
  const detalle = detalles.length > 0
    ? detalles.map((d) => d.texto).join(' ')
    : error?.traceId
      ? `Referencia: ${error.traceId.slice(0, 16)}`
      : '';

  avisar(error?.message ?? 'Ocurrió un error inesperado.', { detalle, tipo: 'error', duracion: 6500 });
}

/* ── Diálogo ────────────────────────────────────────────────────────────────────────────────── */

const dialogo = () => document.getElementById('dialogo');

/**
 * Abre el diálogo con un formulario y resuelve con los datos cuando se envía, o con `null` si se
 * cancela. Devolver una promesa deja el código de la vista lineal y fácil de seguir.
 */
export function abrirFormulario({ titulo, cuerpo, textoAceptar = 'Guardar', alEnviar }) {
  const caja = dialogo();
  document.getElementById('dialogo-titulo').textContent = titulo;

  const contenedor = document.getElementById('dialogo-cuerpo');
  contenedor.innerHTML = html`<form id="forma-dialogo" class="forma" novalidate>
    ${crudo(cuerpo)}
    <p class="mensaje mensaje--error" data-error hidden></p>
    <div class="forma__pie">
      <button class="boton" type="button" data-cancelar>Cancelar</button>
      <button class="boton boton--primario" type="submit" data-envio>${textoAceptar}</button>
    </div>
  </form>`;

  const forma = contenedor.querySelector('form');
  const errorGlobal = forma.querySelector('[data-error]');
  const botonEnvio = forma.querySelector('[data-envio]');

  return new Promise((resolver) => {
    let resuelto = false;

    const cerrar = (resultado) => {
      if (resuelto) return;
      resuelto = true;
      caja.close();
      resolver(resultado);
    };

    forma.querySelector('[data-cancelar]').addEventListener('click', () => cerrar(null));
    caja.addEventListener('close', () => cerrar(null), { once: true });

    forma.addEventListener('submit', async (evento) => {
      evento.preventDefault();
      errorGlobal.hidden = true;
      forma.querySelectorAll('[data-campo-error]').forEach((n) => n.remove());
      forma.querySelectorAll('[aria-invalid]').forEach((n) => n.removeAttribute('aria-invalid'));

      const datos = Object.fromEntries(new FormData(forma).entries());
      botonEnvio.disabled = true;
      botonEnvio.textContent = 'Guardando…';

      try {
        const resultado = await alEnviar(datos);
        cerrar(resultado ?? true);
      } catch (error) {
        mostrarErrorEnFormulario(forma, errorGlobal, error);
      } finally {
        botonEnvio.disabled = false;
        botonEnvio.textContent = textoAceptar;
      }
    });

    caja.showModal();
    forma.querySelector('input, select, textarea')?.focus();
  });
}

/** Reparte los errores de validación bajo su campo y deja el resto en el mensaje general. */
function mostrarErrorEnFormulario(forma, errorGlobal, error) {
  const detalles = error?.detallesValidacion ?? [];
  const huerfanos = [];

  for (const { campo, texto } of detalles) {
    const nombre = campo.charAt(0).toLowerCase() + campo.slice(1);
    const entrada = forma.querySelector(`[name="${CSS.escape(nombre)}"]`);

    if (!entrada) {
      huerfanos.push(texto);
      continue;
    }

    entrada.setAttribute('aria-invalid', 'true');
    const nota = document.createElement('p');
    nota.className = 'campo__error';
    nota.dataset.campoError = '';
    nota.textContent = texto;
    entrada.after(nota);
  }

  const general = huerfanos.length > 0 ? huerfanos.join(' ') : detalles.length > 0 ? '' : error.message;

  if (general) {
    errorGlobal.textContent = general;
    errorGlobal.hidden = false;
  }
}

/** Confirmación modal para acciones destructivas. */
export function confirmar({ titulo, mensaje, textoAceptar = 'Confirmar', peligroso = false }) {
  const caja = dialogo();
  document.getElementById('dialogo-titulo').textContent = titulo;
  document.getElementById('dialogo-cuerpo').innerHTML = html`
    <p>${mensaje}</p>
    <div class="forma__pie">
      <button class="boton" type="button" data-cancelar>Cancelar</button>
      <button class="boton ${crudo(peligroso ? 'boton--peligro' : 'boton--primario')}" type="button" data-aceptar>
        ${textoAceptar}
      </button>
    </div>`;

  return new Promise((resolver) => {
    let resuelto = false;
    const cerrar = (valor) => {
      if (resuelto) return;
      resuelto = true;
      caja.close();
      resolver(valor);
    };

    document.getElementById('dialogo-cuerpo').querySelector('[data-cancelar]').addEventListener('click', () => cerrar(false));
    document.getElementById('dialogo-cuerpo').querySelector('[data-aceptar]').addEventListener('click', () => cerrar(true));
    caja.addEventListener('close', () => cerrar(false), { once: true });

    caja.showModal();
  });
}

/* ── Tablas y paginación ────────────────────────────────────────────────────────────────────── */

/** Pie de paginación. Los botones se activan con `data-pagina` desde la vista. */
export function paginacion(pagina) {
  if (!pagina || pagina.total === 0) return '';

  const desde = (pagina.pagina - 1) * pagina.tamano + 1;
  const hasta = Math.min(pagina.pagina * pagina.tamano, pagina.total);

  return html`<div class="paginacion">
    <span>Mostrando ${String(desde)}–${String(hasta)} de ${String(pagina.total)}</span>
    <div class="paginacion__botones">
      <button class="boton boton--chico" data-pagina="${String(pagina.pagina - 1)}"
              ${crudo(pagina.tieneAnterior ? '' : 'disabled')}>Anterior</button>
      <button class="boton boton--chico" data-pagina="${String(pagina.pagina + 1)}"
              ${crudo(pagina.tieneSiguiente ? '' : 'disabled')}>Siguiente</button>
    </div>
  </div>`;
}

/** Opciones de un desplegable, marcando la seleccionada. */
export function opciones(valores, seleccionado) {
  return crudo(valores.map(([valor, etiqueta]) =>
    `<option value="${esc(valor)}"${String(valor) === String(seleccionado ?? '') ? ' selected' : ''}>${esc(etiqueta)}</option>`,
  ).join(''));
}

/** Atajo: `<option selected>` cuando el valor coincide. */
export const seleccionado = (a, b) => crudo(String(a) === String(b) ? 'selected' : '');

/** Atajo: `checked` cuando la condición se cumple. */
export const marcado = (condicion) => crudo(condicion ? 'checked' : '');
