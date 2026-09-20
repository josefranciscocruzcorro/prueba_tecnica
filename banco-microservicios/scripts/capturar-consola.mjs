/**
 * Genera las capturas de la consola web que acompañan al README.
 *
 *   node scripts/capturar-consola.mjs
 *
 * Conduce un Chrome o Edge ya instalado en modo headless hablando el protocolo DevTools por
 * WebSocket. Se hace así, y no con Playwright o Puppeteer, para no añadir al repositorio una
 * dependencia de 150 MB que solo sirve para tomar unas imágenes de documentación.
 *
 * Requiere el stack levantado: docker compose up -d
 */

import { spawn } from 'node:child_process';
import { mkdir, writeFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { setTimeout as esperar } from 'node:timers/promises';
import path from 'node:path';

const BASE = 'http://localhost:5080';
const DESTINO = path.resolve(import.meta.dirname, '..', 'docs', 'capturas');
const PUERTO_DEPURACION = 9333;
const ANCHO = 1440;
const ALTO = 900;

const NAVEGADORES = [
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
  'C:/Program Files/Microsoft/Edge/Application/msedge.exe',
];

/** Cada captura: archivo, ruta de la consola y alto propio si la vista es más larga. */
const VISTAS = [
  { archivo: '01-acceso.png', ruta: '', sesion: false, espera: 1200 },
  { archivo: '02-resumen.png', ruta: '#/resumen', alto: 1000 },
  { archivo: '03-clientes.png', ruta: '#/clientes' },
  { archivo: '04-cuentas.png', ruta: '#/cuentas' },
  { archivo: '05-movimientos.png', ruta: '#/movimientos' },
  { archivo: '06-reportes.png', ruta: '#/reportes', alto: 1100, previo: 'febrero2022' },
  { archivo: '07-saldo-no-disponible.png', ruta: '#/movimientos', previo: 'saldoNoDisponible' },
  { archivo: '08-resumen-oscuro.png', ruta: '#/resumen', alto: 1000, oscuro: true },
];

function localizarNavegador() {
  const encontrado = NAVEGADORES.find((ruta) => existsSync(ruta));

  if (!encontrado) {
    throw new Error('No se encontró Chrome ni Edge instalados. Instale uno o tome las capturas a mano.');
  }

  return encontrado;
}

async function abrirNavegador() {
  const ejecutable = localizarNavegador();

  const proceso = spawn(ejecutable, [
    '--headless=new',
    `--remote-debugging-port=${PUERTO_DEPURACION}`,
    `--window-size=${ANCHO},${ALTO}`,
    '--hide-scrollbars',
    '--force-device-scale-factor=2',
    '--no-first-run',
    '--no-default-browser-check',
    '--disable-gpu',
    `--user-data-dir=${path.join(process.env.TEMP ?? '/tmp', 'perfil-capturas-banco')}`,
    'about:blank',
  ], { stdio: 'ignore', detached: false });

  // El puerto de depuración tarda un momento en aceptar conexiones.
  for (let intento = 0; intento < 40; intento++) {
    try {
      const respuesta = await fetch(`http://127.0.0.1:${PUERTO_DEPURACION}/json/version`);
      const info = await respuesta.json();
      return { proceso, wsUrl: info.webSocketDebuggerUrl };
    } catch {
      await esperar(250);
    }
  }

  proceso.kill();
  throw new Error('El navegador no expuso el puerto de depuración a tiempo.');
}

/** Cliente mínimo del protocolo DevTools sobre el WebSocket nativo de Node. */
function conectar(wsUrl) {
  const socket = new WebSocket(wsUrl);
  const pendientes = new Map();
  let siguienteId = 1;

  socket.addEventListener('message', (evento) => {
    const mensaje = JSON.parse(evento.data);
    const pendiente = pendientes.get(mensaje.id);

    if (!pendiente) {
      return;
    }

    pendientes.delete(mensaje.id);
    mensaje.error ? pendiente.rechazar(new Error(mensaje.error.message)) : pendiente.resolver(mensaje.result);
  });

  const listo = new Promise((resolver, rechazar) => {
    socket.addEventListener('open', resolver, { once: true });
    socket.addEventListener('error', () => rechazar(new Error('No se pudo abrir el WebSocket.')), { once: true });
  });

  const enviar = (method, params = {}, sessionId) => new Promise((resolver, rechazar) => {
    const id = siguienteId++;
    pendientes.set(id, { resolver, rechazar });
    socket.send(JSON.stringify({ id, method, params, sessionId }));
  });

  return { socket, listo, enviar };
}

async function main() {
  await mkdir(DESTINO, { recursive: true });

  const { proceso, wsUrl } = await abrirNavegador();
  const { socket, listo, enviar } = conectar(wsUrl);
  await listo;

  try {
    const { targetId } = await enviar('Target.createTarget', { url: 'about:blank' });
    const { sessionId } = await enviar('Target.attachToTarget', { targetId, flatten: true });

    const cdp = (method, params) => enviar(method, params, sessionId);

    await cdp('Page.enable');
    await cdp('Runtime.enable');
    await cdp('Emulation.setDeviceMetricsOverride', {
      width: ANCHO, height: ALTO, deviceScaleFactor: 2, mobile: false,
    });

    const evaluar = async (expresion) => {
      const { result, exceptionDetails } = await cdp('Runtime.evaluate', {
        expression: expresion, awaitPromise: true, returnByValue: true,
      });

      if (exceptionDetails) {
        throw new Error(exceptionDetails.exception?.description ?? 'Fallo al evaluar en la página.');
      }

      return result.value;
    };

    // Se inicia sesión una vez llamando a la API y dejando el resultado donde lo busca la consola.
    await cdp('Page.navigate', { url: BASE });
    await esperar(1500);

    const sesion = await evaluar(`
      fetch('/api/clientes/autenticar', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ clienteId: 'CLI-002', contrasena: '5678' }),
      }).then((r) => r.json())
    `);

    if (!sesion?.token) {
      throw new Error('No se pudo iniciar sesión. ¿Está el stack levantado (docker compose up -d)?');
    }

    for (const vista of VISTAS) {
      await cdp('Emulation.setEmulatedMedia', {
        features: [{ name: 'prefers-color-scheme', value: vista.oscuro ? 'dark' : 'light' }],
      });

      await cdp('Emulation.setDeviceMetricsOverride', {
        width: ANCHO, height: vista.alto ?? ALTO, deviceScaleFactor: 2, mobile: false,
      });

      await cdp('Page.navigate', { url: `${BASE}/${vista.ruta}` });
      await esperar(600);

      await evaluar(vista.sesion === false
        ? `localStorage.removeItem('banco.sesion'); 'sin sesion'`
        : `localStorage.setItem('banco.sesion', ${JSON.stringify(JSON.stringify(sesion))}); 'con sesion'`);

      // Recargar de verdad: navegar a la misma URL cambiando solo el fragmento no reinicia la
      // pagina, y la sesion recien inyectada no se leeria.
      await cdp('Page.reload', { ignoreCache: true });
      await esperar(vista.espera ?? 2200);

      if (vista.previo === 'febrero2022') {
        await evaluar(`document.querySelector('[data-febrero]')?.click(); 'ok'`);
        await esperar(1800);
      }

      if (vista.previo === 'saldoNoDisponible') {
        await evaluar(`
          (async () => {
            document.querySelector('[data-nuevo]').click();
            await new Promise((r) => setTimeout(r, 400));
            const f = document.getElementById('forma-dialogo');
            f.querySelector('[name=numeroCuenta]').value = '496825';
            f.querySelector('[name=tipoMovimiento]').value = 'Retiro';
            f.querySelector('[name=valor]').value = '100';
            f.requestSubmit();
            return 'enviado';
          })()
        `);
        await esperar(2000);
      }

      const { data } = await cdp('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false });
      await writeFile(path.join(DESTINO, vista.archivo), Buffer.from(data, 'base64'));

      console.log(`  ✓ ${vista.archivo}`);
    }

    console.log(`\nCapturas escritas en ${path.relative(process.cwd(), DESTINO)}`);
  } finally {
    socket.close();
    proceso.kill();
  }
}

main().catch((error) => {
  console.error(`\nNo se pudieron generar las capturas: ${error.message}`);
  process.exitCode = 1;
});
