-- Se ejecuta una sola vez, al inicializar el volumen de PostgreSQL.
--
-- Cada microservicio tiene su propia base de datos: no comparten tablas ni pueden consultarse
-- entre sí por SQL. Toda la información que necesitan del otro contexto viaja por eventos.
-- El esquema de cada base lo crean las migraciones de Entity Framework Core al arrancar la API;
-- el equivalente en SQL plano está en db/BaseDatos.sql para quien prefiera montarlo a mano.

CREATE DATABASE clientes_db WITH OWNER = banco ENCODING = 'UTF8';
CREATE DATABASE cuentas_db  WITH OWNER = banco ENCODING = 'UTF8';

COMMENT ON DATABASE clientes_db IS 'Maestro de Personas y Clientes.';
COMMENT ON DATABASE cuentas_db  IS 'Cuentas, Movimientos y réplica de lectura de Clientes.';
