/**
 * Vistas de la consola. Cada una se encarga de pedir sus datos, pintarse y reaccionar a los
 * eventos de su propio subárbol; ninguna conoce a las demás. El enrutador de `app.js` decide cuál
 * se muestra y les pasa el contenedor y el estado de la sesión.
 */

import { apiClientes, apiCuentas, apiMovimientos, apiReportes } from './api.js';
import {
  abrirFormulario, avisoError, avisoExito, avisoInfo, cargando, confirmar, crudo, distintivo,
  distintivoEstado, esc, formato, html, importeConColor, marcado, opciones, paginacion, seleccionado, vacio,
} from './ui.js';

const GENEROS = [['Masculino', 'Masculino'], ['Femenino', 'Femenino'], ['Otro', 'Otro'], ['NoDeclarado', 'Prefiero no decirlo']];
const TIPOS_CUENTA = [['Ahorros', 'Ahorros'], ['Corriente', 'Corriente']];

/** Cache breve de la lista de clientes: la usan tres formularios y no cambia a cada segundo. */
let clientesEnCache = null;

async function clientesParaSelector() {
  if (clientesEnCache) return clientesEnCache;

  const pagina = await apiClientes.listar({ tamano: 200 });
  clientesEnCache = pagina.elementos;
  return clientesEnCache;
}

function invalidarCacheClientes() {
  clientesEnCache = null;
}

/* ══ Resumen ═══════════════════════════════════════════════════════════════════════════════ */

export async function vistaResumen(contenedor, { cliente }) {
  contenedor.innerHTML = cargando();

  try {
    const [cuentas, movimientos, actividad, clientes] = await Promise.all([
      apiCuentas.listar({ cliente: cliente.clienteId, tamano: 50 }),
      apiMovimientos.listar({ cliente: cliente.clienteId, tamano: 8 }),
      apiClientes.actividad(cliente.clienteId, 8).catch(() => []),
      apiClientes.listar({ tamano: 1 }),
    ]);

    const saldoTotal = cuentas.elementos.reduce((suma, c) => suma + Number(c.saldoDisponible), 0);
    const activas = cuentas.elementos.filter((c) => c.estado).length;

    contenedor.innerHTML = html`
      <section class="rejilla-kpi">
        ${crudo(tarjetaKpi('Saldo disponible', formato.dinero(saldoTotal), `${activas} de ${cuentas.total} cuentas activas`))}
        ${crudo(tarjetaKpi('Mis cuentas', String(cuentas.total), 'Cuentas a su nombre'))}
        ${crudo(tarjetaKpi('Movimientos', String(movimientos.total), 'Registrados en total'))}
        ${crudo(tarjetaKpi('Clientes', String(clientes.total), 'En el maestro de clientes'))}
      </section>

      <section class="tarjeta">
        <header class="tarjeta__cabecera">
          <h2 class="tarjeta__titulo">Mis cuentas</h2>
          <a class="tarjeta__nota" href="#/cuentas">Ver todas</a>
        </header>
        ${crudo(cuentas.elementos.length === 0
          ? vacio('Todavía no tiene cuentas', 'Cree una desde la sección Cuentas.')
          : tabla(
            ['Número', 'Tipo', { texto: 'Saldo inicial', numero: true }, { texto: 'Saldo disponible', numero: true }, 'Estado'],
            cuentas.elementos.map((c) => [
              `<span class="principal-celda numero">${esc(c.numeroCuenta)}</span>`,
              esc(c.tipoCuenta),
              { valor: formato.dinero(c.saldoInicial), numero: true },
              { valor: `<strong>${esc(formato.dinero(c.saldoDisponible))}</strong>`, numero: true },
              distintivoEstado(c.estado).__html,
            ])))}
      </section>

      <section class="tarjeta">
        <header class="tarjeta__cabecera">
          <h2 class="tarjeta__titulo">Actividad reciente</h2>
          <span class="tarjeta__nota">Recibida por eventos desde el servicio de Cuentas</span>
        </header>
        ${crudo(actividad.length === 0
          ? vacio('Sin actividad todavía', 'Registre un movimiento y aparecerá aquí en cuanto se propague el evento.')
          : `<div class="lista-actividad">${actividad.map(filaActividad).join('')}</div>`)}
      </section>`;
  } catch (error) {
    contenedor.innerHTML = vacio('No se pudo cargar el resumen', error.message);
    avisoError(error);
  }
}

function tarjetaKpi(etiqueta, valor, pie) {
  return html`<article class="tarjeta kpi">
    <p class="kpi__etiqueta">${etiqueta}</p>
    <p class="kpi__valor">${valor}</p>
    <p class="kpi__pie">${pie}</p>
  </article>`;
}

function filaActividad(entrada) {
  return html`<div class="actividad">
    <span class="actividad__punto" aria-hidden="true"></span>
    <span class="actividad__texto">${entrada.descripcion}</span>
    <span class="actividad__fecha">${formato.fechaHora(entrada.ocurridoEn)}</span>
  </div>`;
}

/* ══ Clientes ══════════════════════════════════════════════════════════════════════════════ */

export async function vistaClientes(contenedor) {
  const estado = { buscar: '', estado: '', pagina: 1, tamano: 10 };

  const pintar = async () => {
    const zona = contenedor.querySelector('[data-tabla]');
    zona.innerHTML = cargando();

    try {
      const pagina = await apiClientes.listar(estado);

      zona.innerHTML = pagina.elementos.length === 0
        ? vacio('Sin resultados', 'Pruebe con otro texto de búsqueda o cree un cliente nuevo.')
        : tabla(
          ['Cliente', 'Nombre', 'Identificación', 'Contacto', 'Estado', ''],
          pagina.elementos.map((c) => [
            `<span class="principal-celda">${esc(c.clienteId)}</span>`,
            `${esc(c.nombre)}<br><span class="secundaria">${esc(c.genero)} · ${esc(c.edad)} años</span>`,
            `<span class="numero">${esc(c.identificacion)}</span>`,
            `${esc(c.telefono)}<br><span class="secundaria">${esc(c.direccion)}</span>`,
            distintivoEstado(c.estado).__html,
            {
              valor: `
                <button class="boton boton--chico" data-editar="${esc(c.clienteId)}">Editar</button>
                <button class="boton boton--chico" data-estado="${esc(c.clienteId)}" data-activo="${c.estado}">
                  ${c.estado ? 'Desactivar' : 'Activar'}
                </button>
                <button class="boton boton--chico boton--peligro" data-eliminar="${esc(c.clienteId)}">Eliminar</button>`,
              acciones: true,
            },
          ])) + paginacion(pagina);

      conectarPaginacion(zona, estado, pintar);

      zona.querySelectorAll('[data-editar]').forEach((boton) =>
        boton.addEventListener('click', () => editarCliente(boton.dataset.editar, pintar)));

      zona.querySelectorAll('[data-estado]').forEach((boton) =>
        boton.addEventListener('click', () => alternarEstadoCliente(boton.dataset.estado, boton.dataset.activo === 'true', pintar)));

      zona.querySelectorAll('[data-eliminar]').forEach((boton) =>
        boton.addEventListener('click', () => eliminarCliente(boton.dataset.eliminar, pintar)));
    } catch (error) {
      zona.innerHTML = vacio('No se pudo cargar el listado', error.message);
      avisoError(error);
    }
  };

  contenedor.innerHTML = html`
    <div class="filtros">
      <div class="campo">
        <label for="f-buscar">Buscar</label>
        <input id="f-buscar" placeholder="Nombre, identificación o código" autocomplete="off">
      </div>
      <div class="campo">
        <label for="f-estado">Estado</label>
        <select id="f-estado">${opciones([['', 'Todos'], ['true', 'Activos'], ['false', 'Inactivos']], '')}</select>
      </div>
      <div class="filtros__separador"></div>
      <button class="boton boton--primario" data-nuevo>Nuevo cliente</button>
    </div>
    <section class="tarjeta" data-tabla></section>`;

  conectarFiltroTexto(contenedor.querySelector('#f-buscar'), (valor) => {
    estado.buscar = valor;
    estado.pagina = 1;
    pintar();
  });

  contenedor.querySelector('#f-estado').addEventListener('change', (evento) => {
    estado.estado = evento.target.value;
    estado.pagina = 1;
    pintar();
  });

  contenedor.querySelector('[data-nuevo]').addEventListener('click', () => crearCliente(pintar));

  await pintar();
}

function camposCliente(cliente = null) {
  return html`
    <div class="forma__fila">
      <div class="campo">
        <label for="c-nombre">Nombre completo</label>
        <input id="c-nombre" name="nombre" value="${cliente?.nombre ?? ''}" required>
      </div>
      <div class="campo">
        <label for="c-identificacion">Identificación</label>
        <input id="c-identificacion" name="identificacion" value="${cliente?.identificacion ?? ''}" required>
      </div>
    </div>
    <div class="forma__fila">
      <div class="campo">
        <label for="c-genero">Género</label>
        <select id="c-genero" name="genero">${opciones(GENEROS, cliente?.genero ?? 'NoDeclarado')}</select>
      </div>
      <div class="campo">
        <label for="c-edad">Edad</label>
        <input id="c-edad" name="edad" type="number" min="18" max="120" value="${cliente?.edad ?? ''}" required>
      </div>
      <div class="campo">
        <label for="c-telefono">Teléfono</label>
        <input id="c-telefono" name="telefono" value="${cliente?.telefono ?? ''}" required>
      </div>
    </div>
    <div class="campo">
      <label for="c-direccion">Dirección</label>
      <input id="c-direccion" name="direccion" value="${cliente?.direccion ?? ''}" required>
    </div>
    <div class="forma__fila">
      <div class="campo">
        <label for="c-contrasena">Contraseña</label>
        <input id="c-contrasena" name="contrasena" type="password"
               placeholder="${cliente ? 'Dejar en blanco para no cambiarla' : 'Mínimo 4 caracteres'}"
               ${crudo(cliente ? '' : 'required')}>
      </div>
      <div class="campo">
        <label for="c-clienteid">Identificador de cliente</label>
        <input id="c-clienteid" name="clienteId" value="${cliente?.clienteId ?? ''}"
               placeholder="Se genera solo si lo deja vacío" ${crudo(cliente ? 'disabled' : '')}>
      </div>
    </div>
    <div class="campo">
      <label><input type="checkbox" name="estado" value="true" ${marcado(cliente ? cliente.estado : true)}
                    style="width:auto;margin-right:8px"> Cliente activo</label>
    </div>`;
}

async function crearCliente(recargar) {
  const resultado = await abrirFormulario({
    titulo: 'Nuevo cliente',
    cuerpo: camposCliente(),
    textoAceptar: 'Crear cliente',
    alEnviar: (datos) => apiClientes.crear({
      nombre: datos.nombre,
      genero: datos.genero,
      edad: Number(datos.edad),
      identificacion: datos.identificacion,
      direccion: datos.direccion,
      telefono: datos.telefono,
      contrasena: datos.contrasena,
      clienteId: datos.clienteId?.trim() || null,
      estado: datos.estado === 'true',
    }),
  });

  if (!resultado) return;

  invalidarCacheClientes();
  avisoExito('Cliente creado', `${resultado.nombre} · ${resultado.clienteId}`);
  await recargar();
}

async function editarCliente(clienteId, recargar) {
  try {
    const cliente = await apiClientes.obtener(clienteId);

    const resultado = await abrirFormulario({
      titulo: `Editar ${cliente.clienteId}`,
      cuerpo: camposCliente(cliente),
      alEnviar: (datos) => apiClientes.actualizar(clienteId, {
        nombre: datos.nombre,
        genero: datos.genero,
        edad: Number(datos.edad),
        identificacion: datos.identificacion,
        direccion: datos.direccion,
        telefono: datos.telefono,
        estado: datos.estado === 'true',
        contrasena: datos.contrasena?.trim() || null,
      }),
    });

    if (!resultado) return;

    invalidarCacheClientes();
    avisoExito('Cliente actualizado', resultado.nombre);
    await recargar();
  } catch (error) {
    avisoError(error);
  }
}

async function alternarEstadoCliente(clienteId, activo, recargar) {
  try {
    await apiClientes.parchear(clienteId, { estado: !activo });
    invalidarCacheClientes();
    avisoExito(activo ? 'Cliente desactivado' : 'Cliente activado', clienteId);
    await recargar();
  } catch (error) {
    avisoError(error);
  }
}

async function eliminarCliente(clienteId, recargar) {
  const confirmado = await confirmar({
    titulo: 'Eliminar cliente',
    mensaje: `Se eliminará ${clienteId} del maestro de clientes. Sus cuentas se conservan como registro `
      + 'contable, pero quedarán bloqueadas para nuevas operaciones. ¿Continuar?',
    textoAceptar: 'Eliminar',
    peligroso: true,
  });

  if (!confirmado) return;

  try {
    await apiClientes.eliminar(clienteId);
    invalidarCacheClientes();
    avisoExito('Cliente eliminado', clienteId);
    await recargar();
  } catch (error) {
    avisoError(error);
  }
}

/* ══ Cuentas ═══════════════════════════════════════════════════════════════════════════════ */

export async function vistaCuentas(contenedor) {
  const estado = { cliente: '', buscar: '', estado: '', pagina: 1, tamano: 10 };
  const clientes = await clientesParaSelector().catch(() => []);

  const pintar = async () => {
    const zona = contenedor.querySelector('[data-tabla]');
    zona.innerHTML = cargando();

    try {
      const pagina = await apiCuentas.listar(estado);

      zona.innerHTML = pagina.elementos.length === 0
        ? vacio('Sin cuentas', 'Cree una cuenta para empezar a registrar movimientos.')
        : tabla(
          ['Número', 'Tipo', 'Titular', { texto: 'Saldo inicial', numero: true }, { texto: 'Saldo disponible', numero: true }, 'Estado', ''],
          pagina.elementos.map((c) => [
            `<span class="principal-celda numero">${esc(c.numeroCuenta)}</span>`,
            distintivo(c.tipoCuenta, c.tipoCuenta === 'Ahorros' ? 'acento' : '').__html,
            `${esc(c.nombreCliente ?? '—')}<br><span class="secundaria">${esc(c.clienteId)}</span>`,
            { valor: formato.dinero(c.saldoInicial), numero: true },
            { valor: `<strong>${esc(formato.dinero(c.saldoDisponible))}</strong>`, numero: true },
            distintivoEstado(c.estado).__html,
            {
              valor: `
                <button class="boton boton--chico" data-movimientos="${esc(c.numeroCuenta)}">Movimientos</button>
                <button class="boton boton--chico" data-editar="${esc(c.numeroCuenta)}">Editar</button>`,
              acciones: true,
            },
          ])) + paginacion(pagina);

      conectarPaginacion(zona, estado, pintar);

      zona.querySelectorAll('[data-editar]').forEach((boton) =>
        boton.addEventListener('click', () => editarCuenta(boton.dataset.editar, pintar)));

      zona.querySelectorAll('[data-movimientos]').forEach((boton) =>
        boton.addEventListener('click', () => {
          window.location.hash = `#/movimientos?cuenta=${encodeURIComponent(boton.dataset.movimientos)}`;
        }));
    } catch (error) {
      zona.innerHTML = vacio('No se pudo cargar el listado', error.message);
      avisoError(error);
    }
  };

  contenedor.innerHTML = html`
    <div class="filtros">
      <div class="campo">
        <label for="f-cliente">Cliente</label>
        <select id="f-cliente">${opciones([['', 'Todos'], ...clientes.map((c) => [c.clienteId, `${c.nombre} (${c.clienteId})`])], '')}</select>
      </div>
      <div class="campo">
        <label for="f-buscar">Número de cuenta</label>
        <input id="f-buscar" placeholder="Ej. 478758" autocomplete="off">
      </div>
      <div class="campo">
        <label for="f-estado">Estado</label>
        <select id="f-estado">${opciones([['', 'Todos'], ['true', 'Activas'], ['false', 'Inactivas']], '')}</select>
      </div>
      <div class="filtros__separador"></div>
      <button class="boton boton--primario" data-nueva>Nueva cuenta</button>
    </div>
    <section class="tarjeta" data-tabla></section>`;

  contenedor.querySelector('#f-cliente').addEventListener('change', (e) => {
    estado.cliente = e.target.value;
    estado.pagina = 1;
    pintar();
  });

  conectarFiltroTexto(contenedor.querySelector('#f-buscar'), (valor) => {
    estado.buscar = valor;
    estado.pagina = 1;
    pintar();
  });

  contenedor.querySelector('#f-estado').addEventListener('change', (e) => {
    estado.estado = e.target.value;
    estado.pagina = 1;
    pintar();
  });

  contenedor.querySelector('[data-nueva]').addEventListener('click', () => crearCuenta(clientes, pintar));

  await pintar();
}

async function crearCuenta(clientes, recargar) {
  const lista = clientes.length > 0 ? clientes : await clientesParaSelector().catch(() => []);

  const resultado = await abrirFormulario({
    titulo: 'Nueva cuenta',
    cuerpo: html`
      <div class="campo">
        <label for="n-cliente">Titular</label>
        <select id="n-cliente" name="clienteId" required>
          ${opciones(lista.filter((c) => c.estado).map((c) => [c.clienteId, `${c.nombre} (${c.clienteId})`]), '')}
        </select>
      </div>
      <div class="forma__fila">
        <div class="campo">
          <label for="n-tipo">Tipo de cuenta</label>
          <select id="n-tipo" name="tipoCuenta">${opciones(TIPOS_CUENTA, 'Ahorros')}</select>
        </div>
        <div class="campo">
          <label for="n-saldo">Saldo inicial</label>
          <input id="n-saldo" name="saldoInicial" type="number" step="0.01" min="0" value="0" required>
        </div>
      </div>
      <div class="campo">
        <label for="n-numero">Número de cuenta</label>
        <input id="n-numero" name="numeroCuenta" inputmode="numeric" placeholder="Se genera solo si lo deja vacío">
        <span class="campo__ayuda">Entre 4 y 20 dígitos.</span>
      </div>
      <div class="campo">
        <label><input type="checkbox" name="estado" value="true" checked style="width:auto;margin-right:8px"> Cuenta activa</label>
      </div>`,
    textoAceptar: 'Abrir cuenta',
    alEnviar: (datos) => apiCuentas.crear({
      clienteId: datos.clienteId,
      tipoCuenta: datos.tipoCuenta,
      saldoInicial: Number(datos.saldoInicial),
      numeroCuenta: datos.numeroCuenta?.trim() || null,
      estado: datos.estado === 'true',
    }),
  });

  if (!resultado) return;

  avisoExito('Cuenta abierta', `${resultado.numeroCuenta} · ${formato.dinero(resultado.saldoDisponible)}`);
  await recargar();
}

async function editarCuenta(numeroCuenta, recargar) {
  try {
    const cuenta = await apiCuentas.obtener(numeroCuenta);

    const resultado = await abrirFormulario({
      titulo: `Cuenta ${cuenta.numeroCuenta}`,
      cuerpo: html`
        <div class="forma__fila">
          <div class="campo">
            <label for="e-tipo">Tipo de cuenta</label>
            <select id="e-tipo" name="tipoCuenta">${opciones(TIPOS_CUENTA, cuenta.tipoCuenta)}</select>
          </div>
          <div class="campo">
            <label for="e-saldo">Saldo inicial</label>
            <input id="e-saldo" name="saldoInicial" type="number" step="0.01" min="0" value="${String(cuenta.saldoInicial)}">
            <span class="campo__ayuda">Solo editable mientras la cuenta no tenga movimientos.</span>
          </div>
        </div>
        <div class="campo">
          <label><input type="checkbox" name="estado" value="true" ${marcado(cuenta.estado)}
                        style="width:auto;margin-right:8px"> Cuenta activa</label>
        </div>
        <p class="mensaje mensaje--info">Saldo disponible actual: <strong>${formato.dinero(cuenta.saldoDisponible)}</strong></p>`,
      alEnviar: (datos) => apiCuentas.actualizar(numeroCuenta, {
        tipoCuenta: datos.tipoCuenta,
        saldoInicial: Number(datos.saldoInicial),
        estado: datos.estado === 'true',
      }),
    });

    if (!resultado) return;

    avisoExito('Cuenta actualizada', resultado.numeroCuenta);
    await recargar();
  } catch (error) {
    avisoError(error);
  }
}

/* ══ Movimientos ═══════════════════════════════════════════════════════════════════════════ */

export async function vistaMovimientos(contenedor, { parametros }) {
  const estado = {
    cuenta: parametros.get('cuenta') ?? '',
    cliente: '',
    desde: '',
    hasta: '',
    pagina: 1,
    tamano: 12,
  };

  const clientes = await clientesParaSelector().catch(() => []);

  const pintar = async () => {
    const zona = contenedor.querySelector('[data-tabla]');
    zona.innerHTML = cargando();

    try {
      const pagina = await apiMovimientos.listar(estado);

      zona.innerHTML = pagina.elementos.length === 0
        ? vacio('Sin movimientos', 'Registre uno con el botón «Nuevo movimiento».')
        : tabla(
          ['Fecha', 'Cuenta', 'Tipo', { texto: 'Valor', numero: true }, { texto: 'Saldo disponible', numero: true }, ''],
          pagina.elementos.map((m) => [
            formato.fechaHora(m.fecha),
            `<span class="principal-celda numero">${esc(m.numeroCuenta)}</span><br><span class="secundaria">${esc(m.tipoCuenta)}</span>`,
            distintivo(m.tipoMovimiento, m.tipoMovimiento === 'Deposito' ? 'activo' : 'inactivo').__html,
            { valor: importeConColor(m.valor).__html, numero: true },
            { valor: formato.dinero(m.saldoDisponible), numero: true },
            {
              valor: `<button class="boton boton--chico" data-corregir="${esc(m.movimientoId)}"
                        data-valor="${esc(m.valor)}" data-fecha="${esc(m.fecha)}">Corregir</button>`,
              acciones: true,
            },
          ])) + paginacion(pagina);

      conectarPaginacion(zona, estado, pintar);

      zona.querySelectorAll('[data-corregir]').forEach((boton) =>
        boton.addEventListener('click', () => corregirMovimiento(boton.dataset, pintar)));
    } catch (error) {
      zona.innerHTML = vacio('No se pudo cargar el listado', error.message);
      avisoError(error);
    }
  };

  contenedor.innerHTML = html`
    <div class="filtros">
      <div class="campo">
        <label for="f-cuenta">Cuenta</label>
        <input id="f-cuenta" value="${estado.cuenta}" placeholder="Ej. 225487" autocomplete="off">
      </div>
      <div class="campo">
        <label for="f-cliente">Cliente</label>
        <select id="f-cliente">${opciones([['', 'Todos'], ...clientes.map((c) => [c.clienteId, c.nombre])], '')}</select>
      </div>
      <div class="campo">
        <label for="f-desde">Desde</label>
        <input id="f-desde" type="date">
      </div>
      <div class="campo">
        <label for="f-hasta">Hasta</label>
        <input id="f-hasta" type="date">
      </div>
      <div class="filtros__separador"></div>
      <button class="boton boton--primario" data-nuevo>Nuevo movimiento</button>
    </div>
    <section class="tarjeta" data-tabla></section>`;

  conectarFiltroTexto(contenedor.querySelector('#f-cuenta'), (valor) => {
    estado.cuenta = valor;
    estado.pagina = 1;
    pintar();
  });

  contenedor.querySelector('#f-cliente').addEventListener('change', (e) => {
    estado.cliente = e.target.value;
    estado.pagina = 1;
    pintar();
  });

  for (const [id, clave] of [['#f-desde', 'desde'], ['#f-hasta', 'hasta']]) {
    contenedor.querySelector(id).addEventListener('change', (e) => {
      // El extremo final incluye el día completo; si no, un movimiento de esa tarde se perdería.
      estado[clave] = e.target.value ? (clave === 'hasta' ? `${e.target.value}T23:59:59` : e.target.value) : '';
      estado.pagina = 1;
      pintar();
    });
  }

  contenedor.querySelector('[data-nuevo]').addEventListener('click', () => registrarMovimiento(estado.cuenta, pintar));

  await pintar();
}

async function registrarMovimiento(cuentaSugerida, recargar) {
  const resultado = await abrirFormulario({
    titulo: 'Nuevo movimiento',
    cuerpo: html`
      <div class="campo">
        <label for="m-cuenta">Número de cuenta</label>
        <input id="m-cuenta" name="numeroCuenta" value="${cuentaSugerida}" inputmode="numeric" required>
      </div>
      <div class="forma__fila">
        <div class="campo">
          <label for="m-tipo">Tipo</label>
          <select id="m-tipo" name="tipoMovimiento">
            ${opciones([['Deposito', 'Depósito'], ['Retiro', 'Retiro']], 'Deposito')}
          </select>
        </div>
        <div class="campo">
          <label for="m-valor">Valor</label>
          <input id="m-valor" name="valor" type="number" step="0.01" min="0.01" required>
          <span class="campo__ayuda">Importe en positivo; el tipo define el signo.</span>
        </div>
      </div>
      <div class="campo">
        <label for="m-fecha">Fecha</label>
        <input id="m-fecha" name="fecha" type="datetime-local">
        <span class="campo__ayuda">Si lo deja vacío se usa la fecha y hora actuales.</span>
      </div>`,
    textoAceptar: 'Registrar',
    alEnviar: (datos) => apiMovimientos.registrar({
      numeroCuenta: datos.numeroCuenta,
      tipoMovimiento: datos.tipoMovimiento,
      valor: Number(datos.valor),
      fecha: datos.fecha ? new Date(datos.fecha).toISOString() : null,
    }),
  });

  if (!resultado) return;

  avisoExito(
    `${resultado.tipoMovimiento} registrado`,
    `${formato.importe(resultado.valor)} · saldo ${formato.dinero(resultado.saldoDisponible)}`);
  await recargar();
}

async function corregirMovimiento({ corregir, valor, fecha }, recargar) {
  const absoluto = Math.abs(Number(valor));
  const tipo = Number(valor) >= 0 ? 'Deposito' : 'Retiro';

  const resultado = await abrirFormulario({
    titulo: 'Corregir movimiento',
    cuerpo: html`
      <p class="mensaje mensaje--aviso">
        Al corregir un apunte se recalculan los saldos de toda la serie posterior de la cuenta.
      </p>
      <div class="forma__fila">
        <div class="campo">
          <label for="k-tipo">Tipo</label>
          <select id="k-tipo" name="tipoMovimiento">
            ${opciones([['Deposito', 'Depósito'], ['Retiro', 'Retiro']], tipo)}
          </select>
        </div>
        <div class="campo">
          <label for="k-valor">Valor</label>
          <input id="k-valor" name="valor" type="number" step="0.01" min="0.01" value="${String(absoluto)}" required>
        </div>
      </div>
      <div class="campo">
        <label for="k-fecha">Fecha</label>
        <input id="k-fecha" name="fecha" type="datetime-local" value="${new Date(fecha).toISOString().slice(0, 16)}">
      </div>`,
    alEnviar: (datos) => apiMovimientos.actualizar(corregir, {
      tipoMovimiento: datos.tipoMovimiento,
      valor: Number(datos.valor),
      fecha: datos.fecha ? new Date(datos.fecha).toISOString() : null,
    }),
  });

  if (!resultado) return;

  avisoExito('Movimiento corregido', `Nuevo saldo: ${formato.dinero(resultado.saldoDisponible)}`);
  await recargar();
}

/* ══ Reportes ══════════════════════════════════════════════════════════════════════════════ */

export async function vistaReportes(contenedor, { cliente }) {
  const clientes = await clientesParaSelector().catch(() => []);
  const hoy = new Date();
  const inicioDeMes = new Date(hoy.getFullYear(), hoy.getMonth(), 1);

  contenedor.innerHTML = html`
    <form class="filtros" id="forma-reporte">
      <div class="campo">
        <label for="r-cliente">Cliente</label>
        <select id="r-cliente" name="cliente" required>
          ${opciones(clientes.map((c) => [c.clienteId, `${c.nombre} (${c.clienteId})`]), cliente.clienteId)}
        </select>
      </div>
      <div class="campo">
        <label for="r-desde">Desde</label>
        <input id="r-desde" name="desde" type="date" value="${formato.iso(inicioDeMes)}" required>
      </div>
      <div class="campo">
        <label for="r-hasta">Hasta</label>
        <input id="r-hasta" name="hasta" type="date" value="${formato.iso(hoy)}" required>
      </div>
      <div class="filtros__separador"></div>
      <button class="boton" type="button" data-febrero>Demo: febrero 2022</button>
      <button class="boton boton--primario" type="submit">Generar</button>
    </form>

    <div id="resultado-reporte"></div>`;

  const forma = contenedor.querySelector('#forma-reporte');
  const salida = contenedor.querySelector('#resultado-reporte');

  // Los datos de ejemplo del enunciado están fechados en febrero de 2022; este atajo ahorra
  // tener que recordarlo al revisar el reporte contra la tabla del caso 5.
  contenedor.querySelector('[data-febrero]').addEventListener('click', () => {
    forma.querySelector('#r-desde').value = '2022-02-01';
    forma.querySelector('#r-hasta').value = '2022-02-28';
    forma.requestSubmit();
  });

  forma.addEventListener('submit', async (evento) => {
    evento.preventDefault();
    const datos = Object.fromEntries(new FormData(forma).entries());
    salida.innerHTML = cargando();

    try {
      const reporte = await apiReportes.estadoDeCuenta(datos.cliente, datos.desde, datos.hasta);
      salida.innerHTML = pintarReporte(reporte);
      conectarAccionesReporte(salida, reporte, datos);
    } catch (error) {
      salida.innerHTML = vacio('No se pudo generar el reporte', error.message);
      avisoError(error);
    }
  });

  forma.requestSubmit();
}

function pintarReporte(reporte) {
  const { cliente, rango, resumen, cuentas } = reporte;

  return html`
    <div class="pila">
      <section class="rejilla-kpi">
        ${crudo(tarjetaKpi('Saldo disponible', formato.dinero(resumen.saldoDisponibleTotal), `${resumen.totalCuentas} cuentas`))}
        ${crudo(tarjetaKpi('Depósitos', formato.dinero(resumen.totalDepositos), 'En el periodo'))}
        ${crudo(tarjetaKpi('Retiros', formato.dinero(Math.abs(resumen.totalRetiros)), 'En el periodo'))}
        ${crudo(tarjetaKpi('Movimientos', String(resumen.totalMovimientos), `${rango.desde} → ${rango.hasta}`))}
      </section>

      <section class="tarjeta">
        <header class="tarjeta__cabecera">
          <div>
            <h2 class="tarjeta__titulo">Estado de cuenta · ${cliente.nombre}</h2>
            <p class="tarjeta__nota">${cliente.clienteId} · identificación ${cliente.identificacion}</p>
          </div>
          <div style="display:flex;gap:8px">
            <button class="boton boton--chico" data-ver-json>Ver JSON</button>
            <button class="boton boton--chico" data-descargar>Descargar</button>
          </div>
        </header>
        <div class="tarjeta__cuerpo">
          ${crudo(cuentas.length === 0
            ? vacio('El cliente no tiene cuentas', 'Abra una cuenta para poder emitir su estado.')
            : cuentas.map(bloqueCuentaReporte).join(''))}
        </div>
      </section>

      <section class="tarjeta" data-json hidden>
        <header class="tarjeta__cabecera"><h2 class="tarjeta__titulo">Respuesta JSON del servicio</h2></header>
        <div class="tarjeta__cuerpo"><pre class="json">${JSON.stringify(reporte, null, 2)}</pre></div>
      </section>
    </div>`;
}

function bloqueCuentaReporte(cuenta) {
  return html`<article class="tarjeta reporte-cuenta">
    <header class="reporte-cuenta__cabecera">
      <span class="reporte-cuenta__numero">${cuenta.numeroCuenta}</span>
      ${distintivo(cuenta.tipoCuenta, cuenta.tipoCuenta === 'Ahorros' ? 'acento' : '')}
      ${distintivoEstado(cuenta.estado)}
      <span class="reporte-cuenta__dato">Saldo inicial <strong>${formato.dinero(cuenta.saldoInicial)}</strong></span>
      <span class="reporte-cuenta__dato">Movimientos del periodo <strong>${formato.importe(cuenta.totalMovimientosPeriodo)}</strong></span>
      <span class="reporte-cuenta__dato">Saldo disponible <strong>${formato.dinero(cuenta.saldoDisponible)}</strong></span>
    </header>
    ${crudo(cuenta.movimientos.length === 0
      ? '<p class="vacio">Sin movimientos en el periodo seleccionado.</p>'
      : tabla(
        ['Fecha', 'Tipo', { texto: 'Movimiento', numero: true }, { texto: 'Saldo disponible', numero: true }],
        cuenta.movimientos.map((m) => [
          formato.fecha(m.fecha),
          esc(m.tipoMovimiento),
          { valor: importeConColor(m.valor).__html, numero: true },
          { valor: formato.dinero(m.saldoDisponible), numero: true },
        ])))}
  </article>`;
}

function conectarAccionesReporte(salida, reporte, datos) {
  const bloqueJson = salida.querySelector('[data-json]');

  salida.querySelector('[data-ver-json]')?.addEventListener('click', (evento) => {
    bloqueJson.hidden = !bloqueJson.hidden;
    evento.target.textContent = bloqueJson.hidden ? 'Ver JSON' : 'Ocultar JSON';
  });

  salida.querySelector('[data-descargar]')?.addEventListener('click', () => {
    const blob = new Blob([JSON.stringify(reporte, null, 2)], { type: 'application/json' });
    const enlace = document.createElement('a');
    enlace.href = URL.createObjectURL(blob);
    enlace.download = `estado-de-cuenta-${datos.cliente}-${datos.desde}_${datos.hasta}.json`;
    enlace.click();
    URL.revokeObjectURL(enlace.href);
    avisoInfo('Reporte descargado', enlace.download);
  });
}

/* ══ Utilidades compartidas ════════════════════════════════════════════════════════════════ */

/**
 * Construye una tabla. Las celdas pueden ser una cadena de HTML ya seguro o un objeto con
 * `{ valor, numero, acciones }` para alinear importes a la derecha y agrupar botones.
 */
function tabla(columnas, filas) {
  const encabezado = columnas.map((columna) => {
    const texto = typeof columna === 'string' ? columna : columna.texto;
    const clase = typeof columna === 'object' && columna.numero ? ' class="numero"' : '';
    return `<th${clase}>${esc(texto)}</th>`;
  }).join('');

  const cuerpo = filas.map((fila) => {
    const celdas = fila.map((celda) => {
      if (typeof celda === 'object' && celda !== null) {
        const clase = celda.numero ? ' class="numero"' : celda.acciones ? ' class="acciones"' : '';
        return `<td${clase}>${celda.valor}</td>`;
      }
      return `<td>${celda}</td>`;
    }).join('');

    return `<tr>${celdas}</tr>`;
  }).join('');

  return `<div class="tabla-envoltura"><table><thead><tr>${encabezado}</tr></thead><tbody>${cuerpo}</tbody></table></div>`;
}

/** Filtro de texto con retardo: evita una petición por cada tecla pulsada. */
function conectarFiltroTexto(entrada, alCambiar, retardo = 300) {
  let temporizador;

  entrada.addEventListener('input', () => {
    clearTimeout(temporizador);
    temporizador = setTimeout(() => alCambiar(entrada.value.trim()), retardo);
  });
}

function conectarPaginacion(zona, estado, recargar) {
  zona.querySelectorAll('[data-pagina]').forEach((boton) =>
    boton.addEventListener('click', () => {
      estado.pagina = Number(boton.dataset.pagina);
      recargar();
    }));
}
