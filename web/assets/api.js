/**
 * Cliente HTTP de la consola.
 *
 * Todas las llamadas salen contra el mismo origen: nginx hace de proxy inverso y reparte
 * /api/clientes hacia un microservicio y /api/cuentas, /api/movimientos y /api/reportes hacia el
 * otro. Desde el navegador la separación es invisible, no hay CORS que resolver y el token de
 * sesión vale para ambos porque comparten emisor y clave de firma.
 */

const CLAVE_SESION = 'banco.sesion';

/** Error de negocio devuelto por la API, ya traducido a algo que la interfaz puede mostrar. */
export class ErrorApi extends Error {
  constructor(mensaje, { estado = 0, codigo = 'ERROR_DESCONOCIDO', errores = null, traceId = null } = {}) {
    super(mensaje);
    this.name = 'ErrorApi';
    this.estado = estado;
    this.codigo = codigo;
    this.errores = errores;
    this.traceId = traceId;
  }

  /** Lista plana de mensajes de validación, lista para pintar bajo cada campo. */
  get detallesValidacion() {
    if (!this.errores) return [];
    return Object.entries(this.errores).flatMap(([campo, mensajes]) =>
      mensajes.map((texto) => ({ campo, texto })));
  }
}

/** Sesión activa, conservada en el almacenamiento local para sobrevivir a una recarga. */
export const sesion = {
  leer() {
    try {
      const crudo = localStorage.getItem(CLAVE_SESION);
      if (!crudo) return null;

      const datos = JSON.parse(crudo);
      // Un token caducado equivale a no tener sesión: se descarta sin molestar al usuario.
      if (!datos?.token || new Date(datos.expiraEnUtc) <= new Date()) {
        localStorage.removeItem(CLAVE_SESION);
        return null;
      }
      return datos;
    } catch {
      return null;
    }
  },

  guardar(datos) {
    try {
      localStorage.setItem(CLAVE_SESION, JSON.stringify(datos));
    } catch {
      // Modo privado o almacenamiento lleno: la sesión seguirá viva en memoria hasta recargar.
    }
  },

  cerrar() {
    try {
      localStorage.removeItem(CLAVE_SESION);
    } catch {
      /* nada que hacer */
    }
  },
};

/** Convierte un objeto en cadena de consulta omitiendo lo vacío. */
export function consulta(parametros) {
  const partes = new URLSearchParams();

  for (const [clave, valor] of Object.entries(parametros ?? {})) {
    if (valor === undefined || valor === null || valor === '') continue;
    partes.set(clave, String(valor));
  }

  const texto = partes.toString();
  return texto ? `?${texto}` : '';
}

async function peticion(ruta, { metodo = 'GET', cuerpo = null, autenticada = true } = {}) {
  const cabeceras = { Accept: 'application/json' };
  if (cuerpo !== null) cabeceras['Content-Type'] = 'application/json';

  if (autenticada) {
    const activa = sesion.leer();
    if (!activa) throw new ErrorApi('Su sesión ha caducado. Vuelva a entrar.', { estado: 401, codigo: 'SESION_CADUCADA' });
    cabeceras.Authorization = `Bearer ${activa.token}`;
  }

  let respuesta;
  try {
    respuesta = await fetch(ruta, { method: metodo, headers: cabeceras, body: cuerpo === null ? null : JSON.stringify(cuerpo) });
  } catch {
    throw new ErrorApi('No se pudo contactar con el servidor. Compruebe que los servicios están levantados.',
      { codigo: 'SIN_CONEXION' });
  }

  if (respuesta.status === 204) return null;

  const texto = await respuesta.text();
  const datos = texto ? intentarJson(texto) : null;

  if (respuesta.ok) return datos;

  if (respuesta.status === 401) {
    sesion.cerrar();
    throw new ErrorApi('Su sesión ha caducado. Vuelva a entrar.', { estado: 401, codigo: 'SESION_CADUCADA' });
  }

  // La API devuelve siempre el mismo contrato de error, así que basta con leerlo.
  throw new ErrorApi(datos?.detail || datos?.title || `Error ${respuesta.status}`, {
    estado: respuesta.status,
    codigo: datos?.codigo ?? 'ERROR_DESCONOCIDO',
    errores: datos?.errores ?? null,
    traceId: datos?.traceId ?? null,
  });
}

function intentarJson(texto) {
  try {
    return JSON.parse(texto);
  } catch {
    return { detail: texto };
  }
}

/** Operaciones del microservicio de Clientes. */
export const apiClientes = {
  autenticar: (clienteId, contrasena) =>
    peticion('/api/clientes/autenticar', { metodo: 'POST', cuerpo: { clienteId, contrasena }, autenticada: false }),

  listar: (filtros) => peticion(`/api/clientes${consulta(filtros)}`),
  obtener: (clienteId) => peticion(`/api/clientes/${encodeURIComponent(clienteId)}`),
  crear: (datos) => peticion('/api/clientes', { metodo: 'POST', cuerpo: datos }),
  actualizar: (clienteId, datos) => peticion(`/api/clientes/${encodeURIComponent(clienteId)}`, { metodo: 'PUT', cuerpo: datos }),
  parchear: (clienteId, datos) => peticion(`/api/clientes/${encodeURIComponent(clienteId)}`, { metodo: 'PATCH', cuerpo: datos }),
  eliminar: (clienteId) => peticion(`/api/clientes/${encodeURIComponent(clienteId)}`, { metodo: 'DELETE' }),
  actividad: (clienteId, limite = 12) => peticion(`/api/clientes/${encodeURIComponent(clienteId)}/actividad?limite=${limite}`),
};

/** Operaciones del microservicio de Cuentas y Movimientos. */
export const apiCuentas = {
  listar: (filtros) => peticion(`/api/cuentas${consulta(filtros)}`),
  obtener: (numero) => peticion(`/api/cuentas/${encodeURIComponent(numero)}`),
  crear: (datos) => peticion('/api/cuentas', { metodo: 'POST', cuerpo: datos }),
  actualizar: (numero, datos) => peticion(`/api/cuentas/${encodeURIComponent(numero)}`, { metodo: 'PUT', cuerpo: datos }),
  parchear: (numero, datos) => peticion(`/api/cuentas/${encodeURIComponent(numero)}`, { metodo: 'PATCH', cuerpo: datos }),
  movimientosDe: (numero, filtros) => peticion(`/api/cuentas/${encodeURIComponent(numero)}/movimientos${consulta(filtros)}`),
};

export const apiMovimientos = {
  listar: (filtros) => peticion(`/api/movimientos${consulta(filtros)}`),
  registrar: (datos) => peticion('/api/movimientos', { metodo: 'POST', cuerpo: datos }),
  actualizar: (id, datos) => peticion(`/api/movimientos/${id}`, { metodo: 'PUT', cuerpo: datos }),
};

export const apiReportes = {
  estadoDeCuenta: (cliente, desde, hasta, formato = 'detallado') =>
    peticion(`/api/reportes${consulta({ cliente, fecha: `${desde},${hasta}`, formato })}`),
};
