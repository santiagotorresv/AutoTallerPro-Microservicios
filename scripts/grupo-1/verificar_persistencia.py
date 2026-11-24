#!/usr/bin/env python3
"""Verifica exclusivamente bases efímeras del Grupo 1; no usa Compose ni .env."""
from pathlib import Path
import json
import os
import secrets
import subprocess
import sys
import time
import uuid

RAIZ = Path(__file__).resolve().parents[2]
ETIQUETA = 'autotaller.grupo1.pruebas'

def ejecutar(args, **opciones):
    return subprocess.run(args, check=True, text=True, capture_output=True, **opciones).stdout.strip()

def principal():
    ejecutar(['docker', 'info', '--format', '{{.ServerVersion}}'])
    sufijo = uuid.uuid4().hex[:10]
    base = 'test_grupo1_' + sufijo
    clave = secrets.token_hex(24)
    contenedores = []
    entorno = os.environ.copy()
    entorno.update({'GRUPO1_PRUEBAS_AISLADAS': '1', 'DOTNET_CLI_TELEMETRY_OPTOUT': '1'})
    servicios = [
        ('postgres', 'postgres:17', 5432, ['/var/lib/postgresql/data'],
         {'POSTGRES_DB': base, 'POSTGRES_USER': 'prueba', 'POSTGRES_PASSWORD': clave},
         ['pg_isready', '-U', 'prueba', '-d', base]),
        ('mysql', 'mysql:8.4', 3306, ['/var/lib/mysql'],
         {'MYSQL_DATABASE': base, 'MYSQL_USER': 'prueba', 'MYSQL_PASSWORD': clave, 'MYSQL_ROOT_PASSWORD': clave},
         ['sh', '-c', 'MYSQL_PWD="$MYSQL_ROOT_PASSWORD" mysql --protocol=TCP -h127.0.0.1 -uroot -Nse "SELECT 1"']),
        ('mongo', 'mongo:8.0', 27017, ['/data/db', '/data/configdb'],
         {'MONGO_INITDB_ROOT_USERNAME': 'prueba', 'MONGO_INITDB_ROOT_PASSWORD': clave},
         ['mongosh', '--quiet', '--eval', "db.getSiblingDB('admin').auth(process.env.MONGO_INITDB_ROOT_USERNAME, process.env.MONGO_INITDB_ROOT_PASSWORD); db.adminCommand('ping')"])
    ]
    puertos = {}
    try:
        for modulo, imagen, puerto, directorios, variables, salud in servicios:
            nombre = f'grupo1-prueba-{modulo}-{sufijo}'
            print(f'Preparando {imagen} en {nombre}…', flush=True)
            args = ['docker', 'run', '-d', '--name', nombre, '--label', f'{ETIQUETA}={sufijo}', '-p', f'127.0.0.1::{puerto}']
            for directorio in directorios:
                args += ['--tmpfs', directorio]
            # Docker recibe solo nombres; toma los valores del entorno del subproceso.
            for variable in variables:
                args += ['-e', variable]
            ejecutar(args + [imagen], env={**os.environ, **variables})
            contenedores.append(nombre)
            limite = time.monotonic() + 180
            while True:
                try:
                    ejecutar(['docker', 'exec', nombre] + salud)
                    break
                except subprocess.CalledProcessError:
                    if time.monotonic() >= limite:
                        raise RuntimeError(f'No se pudo iniciar {modulo} a tiempo.')
                    time.sleep(2)
            puerto_host = ejecutar(['docker', 'port', nombre, str(puerto)]).rsplit(':', 1)[1]
            puertos[modulo] = puerto_host
            # Aplica todos los scripts de esquema en orden (001, 002, 003...), igual que infra/levantar-entorno.cjs.
            if modulo == 'postgres':
                for archivo in sorted((RAIZ / 'microservices/clientes-vehiculos/AutoTallerPro.ClientesVehiculos.Infrastructure/Esquema').glob('*.sql')):
                    ejecutar(['docker', 'exec', '-i', nombre, 'psql', '-U', 'prueba', '-d', base, '-v', 'ON_ERROR_STOP=1'], input=archivo.read_text(encoding='utf-8'))
            elif modulo == 'mysql':
                for archivo in sorted((RAIZ / 'microservices/servicios/AutoTallerPro.Servicios.Infrastructure/Esquema').glob('*.sql')):
                    ejecutar(['docker', 'exec', '-i', nombre, 'sh', '-c', 'MYSQL_PWD="$MYSQL_PASSWORD" mysql --default-character-set=utf8mb4 --protocol=TCP -h127.0.0.1 -uprueba "$MYSQL_DATABASE"'], input=archivo.read_text(encoding='utf-8'))
            if modulo == 'mongo':
                esquema = (RAIZ / 'microservices/mecanicos/AutoTallerPro.Mecanicos.Infrastructure/Esquema/001_mecanicos.js').read_text(encoding='utf-8')
                prefijo = f"db.getSiblingDB('admin').auth(process.env.MONGO_INITDB_ROOT_USERNAME, process.env.MONGO_INITDB_ROOT_PASSWORD); db = db.getSiblingDB('{base}');\n"
                for _ in range(2):
                    ejecutar(['docker', 'exec', nombre, 'mongosh', '--quiet', '--eval', prefijo + esquema])
        entorno['GRUPO1_POSTGRES'] = f'Host=127.0.0.1;Port={puertos["postgres"]};Database={base};Username=prueba;Password={clave}'
        entorno['GRUPO1_MYSQL'] = f'Server=127.0.0.1;Port={puertos["mysql"]};Database={base};User ID=prueba;Password={clave}'
        entorno['GRUPO1_MONGO'] = f'mongodb://prueba:{clave}@127.0.0.1:{puertos["mongo"]}/{base}?authSource=admin'
        print('Ejecutando pruebas con PostgreSQL 17, MySQL 8.4 y MongoDB 8.0…', flush=True)
        return subprocess.run(['dotnet', 'test', 'tests/AutoTallerPro.GrupoUno.Tests', '--no-restore', '--disable-build-servers', '-m:1', '-nr:false', '--logger', 'trx;LogFileName=grupo-1-persistencia.trx'], cwd=RAIZ, env=entorno).returncode
    finally:
        for nombre in reversed(contenedores):
            etiquetas = json.loads(ejecutar(['docker', 'inspect', '--format', '{{json .Config.Labels}}', nombre]))
            if etiquetas.get(ETIQUETA) == sufijo:
                ejecutar(['docker', 'rm', '-f', nombre])
                print(f'Retirado el contenedor de prueba {nombre}.', flush=True)

if __name__ == '__main__':
    try:
        sys.exit(principal())
    except subprocess.CalledProcessError as error:
        print('Falló un comando de preparación. Revise la disponibilidad de Docker y las imágenes.', file=sys.stderr)
        sys.exit(error.returncode)
