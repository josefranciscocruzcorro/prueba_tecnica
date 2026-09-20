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
 WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'clientes_db')\gexec

SELECT 'CREATE DATABASE cuentas_db WITH OWNER = banco ENCODING = ''UTF8'''
 WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'cuentas_db')\gexec


-- =============================================================================================
--  2. BASE clientes_db
-- =============================================================================================
--  personas            Datos de la persona fisica. Clave primaria persona_id (uuid).
--  clientes            Hereda de personas por su clave primaria. Anade cliente_id, la contrasena
--                      derivada con PBKDF2 y el estado operativo.
--  actividad_cliente   Bitacora alimentada por los eventos que publica el servicio de Cuentas.
--                      Su clave primaria es el id del evento, lo que la hace idempotente.
--  bandeja_salida      Eventos pendientes de publicar en RabbitMQ.

\connect clientes_db

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'clientes') THEN
        CREATE SCHEMA clientes;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS clientes.__historial_migraciones (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___historial_migraciones" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM clientes.__historial_migraciones WHERE "MigrationId" = '20260919214113_EsquemaInicial') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'clientes') THEN
            CREATE SCHEMA clientes;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM clientes.__historial_migraciones WHERE "MigrationId" = '20260919214113_EsquemaInicial') THEN
    CREATE TABLE clientes.actividad_cliente (
        evento_id uuid NOT NULL,
        cliente_id character varying(30) NOT NULL,
        tipo character varying(30) NOT NULL,
        descripcion character varying(300) NOT NULL,
        ocurrido_en timestamp with time zone NOT NULL,
        CONSTRAINT "PK_actividad_cliente" PRIMARY KEY (evento_id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM clientes.__historial_migraciones WHERE "MigrationId" = '20260919214113_EsquemaInicial') THEN
    CREATE TABLE clientes.bandeja_salida (
        mensaje_id uuid NOT NULL,
        tipo character varying(250) NOT NULL,
        contenido text NOT NULL,
        creado_en timestamp with time zone NOT NULL,
        publicado_en timestamp with time zone,
        intentos integer NOT NULL,
        ultimo_error character varying(500),
        CONSTRAINT "PK_bandeja_salida" PRIMARY KEY (mensaje_id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM clientes.__historial_migraciones WHERE "MigrationId" = '20260919214113_EsquemaInicial') THEN
    CREATE TABLE clientes.personas (
        persona_id uuid NOT NULL,
        nombre character varying(120) NOT NULL,
        genero character varying(20) NOT NULL,
        edad integer NOT NULL,
        identificacion character varying(20) NOT NULL,
        direccion character varying(200) NOT NULL,
        telefono character varying(20) NOT NULL,
        CONSTRAINT "PK_personas" PRIMARY KEY (persona_id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM clientes.__historial_migraciones WHERE "MigrationId" = '20260919214113_EsquemaInicial') THEN
    CREATE TABLE clientes.clientes (
        persona_id uuid NOT NULL,
        cliente_id character varying(30) NOT NULL,
        contrasena_hash character varying(256) NOT NULL,
        estado boolean NOT NULL,
        creado_en timestamp with time zone NOT NULL,
        actualizado_en timestamp with time zone NOT NULL,
        CONSTRAINT "PK_clientes" PRIMARY KEY (persona_id),
        CONSTRAINT "FK_clientes_personas_persona_id" FOREIGN KEY (persona_id) REFERENCES clientes.personas (persona_id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM clientes.__historial_migraciones WHERE "MigrationId" = '20260919214113_EsquemaInicial') THEN
    CREATE INDEX ix_actividad_cliente_fecha ON clientes.actividad_cliente (cliente_id, ocurrido_en);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM clientes.__historial_migraciones WHERE "MigrationId" = '20260919214113_EsquemaInicial') THEN
    CREATE INDEX ix_bandeja_salida_pendientes ON clientes.bandeja_salida (creado_en) WHERE publicado_en IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM clientes.__historial_migraciones WHERE "MigrationId" = '20260919214113_EsquemaInicial') THEN
    CREATE INDEX ix_clientes_estado ON clientes.clientes (estado);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM clientes.__historial_migraciones WHERE "MigrationId" = '20260919214113_EsquemaInicial') THEN
    CREATE UNIQUE INDEX ux_clientes_cliente_id ON clientes.clientes (cliente_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM clientes.__historial_migraciones WHERE "MigrationId" = '20260919214113_EsquemaInicial') THEN
    CREATE INDEX ix_personas_nombre ON clientes.personas (nombre);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM clientes.__historial_migraciones WHERE "MigrationId" = '20260919214113_EsquemaInicial') THEN
    CREATE UNIQUE INDEX ux_personas_identificacion ON clientes.personas (identificacion);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM clientes.__historial_migraciones WHERE "MigrationId" = '20260919214113_EsquemaInicial') THEN
    INSERT INTO clientes.__historial_migraciones ("MigrationId", "ProductVersion")
    VALUES ('20260919214113_EsquemaInicial', '10.0.10');
    END IF;
END $EF$;
COMMIT;



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

\connect cuentas_db

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'cuentas') THEN
        CREATE SCHEMA cuentas;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS cuentas.__historial_migraciones (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___historial_migraciones" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM cuentas.__historial_migraciones WHERE "MigrationId" = '20260919215039_EsquemaInicial') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'cuentas') THEN
            CREATE SCHEMA cuentas;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM cuentas.__historial_migraciones WHERE "MigrationId" = '20260919215039_EsquemaInicial') THEN
    CREATE TABLE cuentas.bandeja_salida (
        mensaje_id uuid NOT NULL,
        tipo character varying(250) NOT NULL,
        contenido text NOT NULL,
        creado_en timestamp with time zone NOT NULL,
        publicado_en timestamp with time zone,
        intentos integer NOT NULL,
        ultimo_error character varying(500),
        CONSTRAINT "PK_bandeja_salida" PRIMARY KEY (mensaje_id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM cuentas.__historial_migraciones WHERE "MigrationId" = '20260919215039_EsquemaInicial') THEN
    CREATE TABLE cuentas.clientes_replicados (
        cliente_id character varying(30) NOT NULL,
        nombre character varying(120) NOT NULL,
        identificacion character varying(20) NOT NULL,
        estado boolean NOT NULL,
        sincronizado_en timestamp with time zone NOT NULL,
        CONSTRAINT "PK_clientes_replicados" PRIMARY KEY (cliente_id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM cuentas.__historial_migraciones WHERE "MigrationId" = '20260919215039_EsquemaInicial') THEN
    CREATE TABLE cuentas.cuentas (
        cuenta_id uuid NOT NULL,
        numero_cuenta character varying(20) NOT NULL,
        tipo_cuenta character varying(20) NOT NULL,
        saldo_inicial numeric(18,2) NOT NULL,
        saldo_disponible numeric(18,2) NOT NULL,
        estado boolean NOT NULL,
        cliente_id character varying(30) NOT NULL,
        creada_en timestamp with time zone NOT NULL,
        CONSTRAINT "PK_cuentas" PRIMARY KEY (cuenta_id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM cuentas.__historial_migraciones WHERE "MigrationId" = '20260919215039_EsquemaInicial') THEN
    CREATE TABLE cuentas.movimientos (
        movimiento_id uuid NOT NULL,
        cuenta_id uuid NOT NULL,
        fecha timestamp with time zone NOT NULL,
        valor numeric(18,2) NOT NULL,
        tipo_movimiento character varying(20) NOT NULL,
        saldo_disponible numeric(18,2) NOT NULL,
        secuencia integer NOT NULL,
        CONSTRAINT "PK_movimientos" PRIMARY KEY (movimiento_id),
        CONSTRAINT "FK_movimientos_cuentas_cuenta_id" FOREIGN KEY (cuenta_id) REFERENCES cuentas.cuentas (cuenta_id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM cuentas.__historial_migraciones WHERE "MigrationId" = '20260919215039_EsquemaInicial') THEN
    CREATE INDEX ix_bandeja_salida_pendientes ON cuentas.bandeja_salida (creado_en) WHERE publicado_en IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM cuentas.__historial_migraciones WHERE "MigrationId" = '20260919215039_EsquemaInicial') THEN
    CREATE INDEX ix_cuentas_cliente ON cuentas.cuentas (cliente_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM cuentas.__historial_migraciones WHERE "MigrationId" = '20260919215039_EsquemaInicial') THEN
    CREATE UNIQUE INDEX ux_cuentas_numero ON cuentas.cuentas (numero_cuenta);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM cuentas.__historial_migraciones WHERE "MigrationId" = '20260919215039_EsquemaInicial') THEN
    CREATE INDEX ix_movimientos_cuenta_fecha ON cuentas.movimientos (cuenta_id, fecha, secuencia);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM cuentas.__historial_migraciones WHERE "MigrationId" = '20260919215039_EsquemaInicial') THEN
    CREATE INDEX ix_movimientos_fecha ON cuentas.movimientos (fecha);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM cuentas.__historial_migraciones WHERE "MigrationId" = '20260919215039_EsquemaInicial') THEN
    INSERT INTO cuentas.__historial_migraciones ("MigrationId", "ProductVersion")
    VALUES ('20260919215039_EsquemaInicial', '10.0.10');
    END IF;
END $EF$;
COMMIT;



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
