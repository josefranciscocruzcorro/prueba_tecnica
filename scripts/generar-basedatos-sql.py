#!/usr/bin/env python3
"""
Genera db/BaseDatos.sql a partir de las migraciones de Entity Framework Core.

Las migraciones son la fuente de verdad del esquema: las APIs las aplican solas al arrancar.
Este script las exporta a un único archivo SQL ejecutable de principio a fin, añade la creación
de las dos bases y los datos iniciales del enunciado, y deja el resultado documentado.

    python scripts/generar-basedatos-sql.py

Requiere la herramienta local dotnet-ef (ya declarada en .config/dotnet-tools.json).
"""

from __future__ import annotations

import io
import subprocess
import sys
import tempfile
from pathlib import Path

RAIZ = Path(__file__).resolve().parent.parent
DESTINO = RAIZ / "db" / "BaseDatos.sql"

SERVICIOS = [
    ("clientes", "src/Clientes/Clientes.Infrastructure", "src/Clientes/Clientes.Api"),
    ("cuentas", "src/Cuentas/Cuentas.Infrastructure", "src/Cuentas/Cuentas.Api"),
]

ENCABEZADO = """\
-- =============================================================================================
--  BaseDatos.sql - Esquema completo y datos iniciales del sistema
--  Ejercicio tecnico - Arquitectura de microservicios (PostgreSQL 17)
--  Autor: Jose Francisco Cruz Corro
-- =============================================================================================
--
--  ORIGEN
--    Generado automaticamente a partir de las migraciones de Entity Framework Core, que son la
--    fuente de verdad del esquema:  python scripts/generar-basedatos-sql.py
--    No editar a mano: los cambios se pierden en la siguiente generacion.
--
--  CUANDO HACE FALTA
--    Con "docker compose up" NO hace falta: cada API aplica sus migraciones y siembra sus datos
--    al arrancar. Este archivo se entrega para revisar el modelo de un vistazo o montar la base
--    manualmente en un servidor PostgreSQL ya existente.
--
--  COMO EJECUTARLO
--    Conectado a la base "postgres" del servidor; el script se encarga del resto:
--      psql -h localhost -p 5442 -U banco -d postgres -f db/BaseDatos.sql
--    Es idempotente: se puede ejecutar varias veces sin duplicar nada.
--
--  CONTENIDO
--    1. Bases de datos          clientes_db y cuentas_db, una por microservicio
--    2. clientes_db             esquema + datos iniciales
--    3. cuentas_db              esquema + datos iniciales
--
--  DECISIONES DE MODELADO QUE CONVIENE CONOCER
--    - Herencia Persona -> Cliente con estrategia tabla-por-tipo: los atributos comunes viven en
--      "personas" y los propios del cliente en "clientes", unidos por la clave primaria. Evita
--      las columnas nulas de la estrategia de tabla unica y refleja la jerarquia en el esquema.
--    - Los importes son numeric(18,2). Nunca coma flotante: el dinero no admite redondeos ciegos.
--    - Cada base tiene su tabla "bandeja_salida" (patron Outbox). Los eventos de integracion se
--      escriben ahi en la misma transaccion que el cambio de negocio y un proceso en segundo
--      plano los publica en RabbitMQ. Es lo que garantiza que no se pierda ningun evento.
--    - "cuentas_db.clientes_replicados" es una replica de solo lectura alimentada por eventos.
--      No hay clave foranea entre bases: los microservicios no comparten datos, los sincronizan.
--    - Los movimientos guardan el saldo resultante de cada apunte ademas del saldo vigente de la
--      cuenta. El primero es historico; el segundo es el dato de consulta.
-- =============================================================================================


-- =============================================================================================
--  1. BASES DE DATOS
-- =============================================================================================
--  Una base por microservicio. No comparten tablas ni pueden consultarse entre si por SQL:
--  toda la informacion que necesitan del otro contexto viaja por eventos.

SELECT 'CREATE DATABASE clientes_db WITH OWNER = banco ENCODING = ''UTF8'''
 WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'clientes_db')\\gexec

SELECT 'CREATE DATABASE cuentas_db WITH OWNER = banco ENCODING = ''UTF8'''
 WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'cuentas_db')\\gexec


-- =============================================================================================
--  2. BASE clientes_db
-- =============================================================================================
--  personas            Datos de la persona fisica. Clave primaria persona_id (uuid).
--  clientes            Hereda de personas por su clave primaria. Anade cliente_id, la contrasena
--                      derivada con PBKDF2 y el estado operativo.
--  actividad_cliente   Bitacora alimentada por los eventos que publica el servicio de Cuentas.
--                      Su clave primaria es el id del evento, lo que la hace idempotente.
--  bandeja_salida      Eventos pendientes de publicar en RabbitMQ.

\\connect clientes_db

"""

DATOS_CLIENTES = """

-- ---------------------------------------------------------------------------------------------
--  2.1 Datos iniciales - Caso 1 del enunciado: creacion de usuarios
-- ---------------------------------------------------------------------------------------------
--  Las contrasenas se guardan derivadas con PBKDF2-HMAC-SHA256 (210.000 iteraciones y sal
--  aleatoria por usuario). Los valores de abajo corresponden a las del enunciado:
--      CLI-001  Jose Lema ............ 1234
--      CLI-002  Marianela Montalvo ... 5678
--      CLI-003  Juan Osorio .......... 1245
--  No son reversibles: para usar otras contrasenas, cree los clientes por la API.

INSERT INTO clientes.personas (persona_id, nombre, genero, edad, identificacion, direccion, telefono) VALUES
  ('05a4b5a0-a36a-4aef-8b5b-c7c54283dbdc', 'Jose Lema',          'Masculino', 35, '1717171717', 'Otavalo sn y principal', '098254785'),
  ('d1863195-9ae0-41d4-bd6a-1b0d5e325319', 'Marianela Montalvo', 'Femenino',  29, '1818181818', 'Amazonas y NNUU',        '097548965'),
  ('69c7bc80-606f-4d8f-ad62-39d2708c90f1', 'Juan Osorio',        'Masculino', 42, '1919191919', '13 junio y Equinoccial', '098874587')
ON CONFLICT (persona_id) DO NOTHING;

INSERT INTO clientes.clientes (persona_id, cliente_id, contrasena_hash, estado, creado_en, actualizado_en) VALUES
  ('05a4b5a0-a36a-4aef-8b5b-c7c54283dbdc', 'CLI-001',
   'pbkdf2-sha256$210000$LOKeN1ie6vuNTI7IL1bE9Q==$Nhs0g0t7VqMurYMFJipeOxtkIcxYYk8yzeGfDnC2QBo=', TRUE, NOW(), NOW()),
  ('d1863195-9ae0-41d4-bd6a-1b0d5e325319', 'CLI-002',
   'pbkdf2-sha256$210000$Na6gnM3H62xvQp42YMkxWQ==$bxKtyHI2YFZOrAl0b6mP5qbnBV/5K/o5CTldZAk1nwU=', TRUE, NOW(), NOW()),
  ('69c7bc80-606f-4d8f-ad62-39d2708c90f1', 'CLI-003',
   'pbkdf2-sha256$210000$ZTHN9vBZBLxPl+8ufHUNQw==$guC+fkZlsSOxLRtMuvMcrKI5h1woiqdsEqRBwYkkdbI=', TRUE, NOW(), NOW())
ON CONFLICT (persona_id) DO NOTHING;


-- =============================================================================================
--  3. BASE cuentas_db
-- =============================================================================================
--  cuentas              Cuenta de ahorros o corriente. saldo_inicial es el de apertura y
--                       saldo_disponible el vigente tras aplicar todos los movimientos.
--  movimientos          Apuntes con valor con signo (positivo deposita, negativo retira) y el
--                       saldo resultante en ese punto de la serie. "secuencia" desempata los
--                       apuntes de la misma fecha para que el saldo acumulado sea determinista.
--  clientes_replicados  Replica local de los clientes, mantenida por eventos. Permite abrir
--                       cuentas y emitir reportes sin llamar al servicio de Clientes.
--  bandeja_salida       Eventos pendientes de publicar en RabbitMQ.

\\connect cuentas_db

"""

DATOS_CUENTAS = """

-- ---------------------------------------------------------------------------------------------
--  3.1 Datos iniciales - Casos 2 a 5 del enunciado
-- ---------------------------------------------------------------------------------------------

--  Replica de clientes. En ejecucion real la llenan los eventos ClienteCreado que publica el
--  otro microservicio; aqui se siembra directamente para no depender del bus.
INSERT INTO cuentas.clientes_replicados (cliente_id, nombre, identificacion, estado, sincronizado_en) VALUES
  ('CLI-001', 'Jose Lema',          '1717171717', TRUE, NOW()),
  ('CLI-002', 'Marianela Montalvo', '1818181818', TRUE, NOW()),
  ('CLI-003', 'Juan Osorio',        '1919191919', TRUE, NOW())
ON CONFLICT (cliente_id) DO NOTHING;

--  Cuentas de los casos 2 y 3. saldo_disponible ya refleja los movimientos de mas abajo.
INSERT INTO cuentas.cuentas (cuenta_id, numero_cuenta, tipo_cuenta, saldo_inicial, saldo_disponible, estado, cliente_id, creada_en) VALUES
  ('644be810-c717-40b2-ac39-80d42e191715', '478758', 'Ahorros',   2000.00, 1425.00, TRUE, 'CLI-001', NOW()),
  ('668bdc3b-a193-405d-8070-150593f9228a', '225487', 'Corriente',  100.00,  700.00, TRUE, 'CLI-002', NOW()),
  ('7cac070a-e6bc-4ff5-af93-ea52f3c69b52', '495878', 'Ahorros',      0.00,  150.00, TRUE, 'CLI-003', NOW()),
  ('59866ecc-af5b-4f69-ac4f-c8961e6605c1', '496825', 'Ahorros',    540.00,    0.00, TRUE, 'CLI-002', NOW()),
  ('79352900-afb7-4d92-8a39-aa0e20cc80f0', '585545', 'Corriente', 1000.00, 1000.00, TRUE, 'CLI-001', NOW())
ON CONFLICT (cuenta_id) DO NOTHING;

--  Movimientos de los casos 4 y 5, fechados en febrero de 2022 para que el reporte F4 sobre ese
--  mes reproduzca exactamente el listado del caso 5:
--      GET /api/reportes?fecha=2022-02-01,2022-02-28&cliente=CLI-002&formato=plano
INSERT INTO cuentas.movimientos (movimiento_id, cuenta_id, fecha, tipo_movimiento, valor, saldo_disponible, secuencia) VALUES
  ('7327fb2d-2676-47c0-8c42-2d3de9546504', '59866ecc-af5b-4f69-ac4f-c8961e6605c1', '2022-02-08 10:00:00+00', 'Retiro',   -540.00,    0.00, 1),
  ('174691d4-ed17-4d39-8509-fdc6c74587cb', '644be810-c717-40b2-ac39-80d42e191715', '2022-02-09 11:30:00+00', 'Retiro',   -575.00, 1425.00, 1),
  ('1aa929f6-bd4e-4ebd-b094-68c68c088447', '668bdc3b-a193-405d-8070-150593f9228a', '2022-02-10 09:15:00+00', 'Deposito',  600.00,  700.00, 1),
  ('74ee4c80-480c-4806-a26c-06c488acbdef', '7cac070a-e6bc-4ff5-af93-ea52f3c69b52', '2022-02-11 16:45:00+00', 'Deposito',  150.00,  150.00, 1)
ON CONFLICT (movimiento_id) DO NOTHING;


-- =============================================================================================
--  Fin de BaseDatos.sql
-- =============================================================================================
"""


def exportar_migraciones(proyecto: str, arranque: str, salida: Path) -> str:
    """Pide a dotnet-ef el script idempotente de un microservicio."""
    subprocess.run(
        [
            "dotnet", "dotnet-ef", "migrations", "script",
            "--idempotent",
            "--project", proyecto,
            "--startup-project", arranque,
            "--no-build",
            "--output", str(salida),
        ],
        cwd=RAIZ,
        check=True,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )

    # dotnet-ef escribe con marca de orden de bytes; se retira para no partir el archivo final.
    return io.open(salida, encoding="utf-8-sig").read()


def main() -> int:
    with tempfile.TemporaryDirectory() as temporal:
        scripts = {
            nombre: exportar_migraciones(proyecto, arranque, Path(temporal) / f"{nombre}.sql")
            for nombre, proyecto, arranque in SERVICIOS
        }

    contenido = (
        ENCABEZADO
        + scripts["clientes"]
        + DATOS_CLIENTES
        + scripts["cuentas"]
        + DATOS_CUENTAS
    )

    DESTINO.write_text(contenido, encoding="utf-8")
    print(f"Escrito {DESTINO.relative_to(RAIZ)} ({contenido.count(chr(10)) + 1} lineas)")

    return 0


if __name__ == "__main__":
    sys.exit(main())
