// Prepara y levanta el entorno local completo en un solo paso. Se puede repetir:
// conserva .env, volúmenes y datos, y solo completa lo que falta.
// Crea la estructura de cada base y, si no existe, el primer administrador de
// Usuarios por invitación. Con --datos-demo agrega los datos de ejemplo de infra/demo.
const { existsSync, readFileSync, writeFileSync } = require('node:fs');
const { join } = require('node:path');
const { spawnSync } = require('node:child_process');
const { randomBytes, randomUUID } = require('node:crypto');
const { parseEnv } = require('node:util');

const raiz = join(__dirname, '..');
process.chdir(raiz);

const proyecto = process.env.COMPOSE_PROJECT_NAME || 'autotaller-ec3';
const realm = JSON.parse(readFileSync(join(__dirname, 'keycloak', 'autotaller-realm.json'), 'utf8'));
const secretosClientes = {
    'autotaller-usuarios-api': 'KEYCLOAK_USUARIOS_CLIENT_SECRET',
    'autotaller-frontend': 'KEYCLOAK_FRONTEND_CLIENT_SECRET'
};

const clienteMysql = 'MYSQL_PWD="$MYSQL_PASSWORD" exec mysql --default-character-set=utf8mb4 --protocol=TCP '
    + '--host=127.0.0.1 --user="$MYSQL_USER" --database="$MYSQL_DATABASE"';
const clientePostgres = 'PGPASSWORD="$POSTGRES_PASSWORD" exec psql -v ON_ERROR_STOP=1 -q -h 127.0.0.1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"';
const clienteMongo = 'archivo=/tmp/esquema-$$.js; cat > "$archivo"; mongosh --quiet --norc --host 127.0.0.1 '
    + '--username "$MONGO_APP_USERNAME" --password "$MONGO_APP_PASSWORD" '
    + '--authenticationDatabase "$MONGO_INITDB_DATABASE" "$MONGO_INITDB_DATABASE" "$archivo"; rm -f "$archivo"';

const esquemas = [
    { modulo: 'Usuarios', servicio: 'mysql-usuarios', cliente: clienteMysql,
        archivos: ['infra/mysql/usuarios/001_persistencia_usuarios.sql',
            'infra/mysql/usuarios/002_altas_usuarios.sql',
            'infra/mysql/usuarios/003_administracion_usuarios.sql'] },
    { modulo: 'Clientes/Vehículos', servicio: 'postgres-clientes', cliente: clientePostgres,
        archivos: ['microservices/clientes-vehiculos/AutoTallerPro.ClientesVehiculos.Infrastructure/Esquema/001_clientes_vehiculos.sql',
            'microservices/clientes-vehiculos/AutoTallerPro.ClientesVehiculos.Infrastructure/Esquema/002_placa_extranjera.sql'] },
    { modulo: 'Servicios', servicio: 'mysql-servicios', cliente: clienteMysql,
        archivos: ['microservices/servicios/AutoTallerPro.Servicios.Infrastructure/Esquema/001_catalogo_servicios.sql'] },
    { modulo: 'Mecánicos', servicio: 'mongo-mecanicos', cliente: clienteMongo,
        archivos: ['microservices/mecanicos/AutoTallerPro.Mecanicos.Infrastructure/Esquema/001_mecanicos.js'] }
];

const datosDemo = [
    { modulo: 'Clientes/Vehículos', servicio: 'postgres-clientes', cliente: clientePostgres, archivo: 'infra/demo/clientes_vehiculos.sql' },
    { modulo: 'Servicios', servicio: 'mysql-servicios', cliente: clienteMysql, archivo: 'infra/demo/servicios.sql' },
    { modulo: 'Mecánicos', servicio: 'mongo-mecanicos', cliente: clienteMongo, archivo: 'infra/demo/mecanicos.js' }
];

class ErrorEntorno extends Error {}

function exigir(condicion, mensaje) {
    if (!condicion) throw new ErrorEntorno(mensaje);
}

function paso(texto) {
    console.log('\n== ' + texto);
}

function ejecutar(comando, argumentos, { entrada, capturar = false, permitirFallo = false } = {}) {
    const resultado = spawnSync(comando, argumentos, {
        input: entrada,
        stdio: [entrada === undefined ? 'inherit' : 'pipe', capturar ? 'pipe' : 'inherit', capturar ? 'pipe' : 'inherit'],
        encoding: capturar ? 'utf8' : undefined
    });
    exigir(!resultado.error, 'No se pudo ejecutar ' + comando + '. ¿Está instalado y en el PATH?');
    const detalle = capturar ? (resultado.stderr || '').trim().split(/\r?\n/).slice(-3).join('\n') : '';
    exigir(permitirFallo || resultado.status === 0, 'Falló: ' + comando + ' ' + argumentos.slice(0, 4).join(' ')
        + (detalle ? '\n' + detalle : ''));
    return resultado;
}

const compose = (argumentos, opciones) => ejecutar('docker', ['compose', ...argumentos], opciones);
const enContenedor = (servicio, script, opciones) =>
    compose(['exec', '-T', servicio, 'sh', '-ec', script], opciones);

function leerArgumentos() {
    const opciones = { admin: 'administrador@autotaller.test', nombre: 'Administrador', apellido: 'Principal', clave: null, demo: false };
    const nombres = { '--admin': 'admin', '--nombre': 'nombre', '--apellido': 'apellido', '--clave': 'clave' };
    const argumentos = process.argv.slice(2);
    for (let i = 0; i < argumentos.length; i++) {
        if (argumentos[i] === '--datos-demo') {
            opciones.demo = true;
            continue;
        }
        const campo = nombres[argumentos[i]];
        exigir(campo && argumentos[i + 1], 'Uso: node infra/levantar-entorno.cjs [--datos-demo] [--admin correo] '
            + '[--nombre N] [--apellido A] [--clave uuid]');
        opciones[campo] = argumentos[++i];
    }
    return opciones;
}

function prepararEnv() {
    const plantilla = readFileSync('.env.example', 'utf8');
    if (!existsSync('.env')) {
        const volumenes = ejecutar('docker', ['volume', 'ls', '-q', '--filter', 'label=com.docker.compose.project=' + proyecto],
            { capturar: true }).stdout.trim();
        exigir(!volumenes, 'Hay volúmenes de ' + proyecto + ' pero falta .env. Recupera tu .env anterior: '
            + 'las contraseñas nuevas no coincidirían con las bases existentes.');
        writeFileSync('.env', plantilla);
        console.log('Se creó .env desde .env.example.');
    }

    const texto = readFileSync('.env', 'utf8');
    const salto = texto.includes('\r\n') ? '\r\n' : '\n';
    const lineas = texto.split(/\r?\n/);
    const presentes = new Set(lineas.map(linea => linea.split('=')[0]));
    for (const linea of plantilla.split(/\r?\n/)) {
        const nombre = linea.split('=')[0];
        if (/^[A-Z0-9_]+=/.test(linea) && !presentes.has(nombre)) lineas.push(linea);
    }

    let generadas = 0;
    const resultado = lineas.map(linea => {
        const [nombre, ...resto] = linea.split('=');
        const vacia = /^[A-Z0-9_]+_PASSWORD$/.test(nombre) && resto.join('=').trim() === '';
        if (!vacia || nombre === 'KEYCLOAK_SMTP_GMAIL_APP_PASSWORD') return linea;
        generadas++;
        return nombre + '=' + randomBytes(18).toString('base64url');
    });
    writeFileSync('.env', resultado.join(salto));
    console.log(generadas ? 'Contraseñas locales generadas: ' + generadas + '.' : 'El .env ya tenía sus contraseñas.');
}

function leerEnv() {
    return { ...parseEnv(readFileSync('.env', 'utf8')), ...process.env };
}

function guardarEnv(nombre, valor) {
    const texto = readFileSync('.env', 'utf8');
    const linea = new RegExp('^' + nombre + '=.*$', 'm');
    writeFileSync('.env', linea.test(texto) ? texto.replace(linea, () => nombre + '=' + valor)
        : texto.replace(/\s*$/, '') + '\n' + nombre + '=' + valor + '\n');
}

async function keycloak(env) {
    const base = 'http://127.0.0.1:' + (env.KEYCLOAK_PORT || 8180);
    const respuesta = await fetch(base + '/realms/master/protocol/openid-connect/token', {
        method: 'POST',
        signal: AbortSignal.timeout(20000),
        body: new URLSearchParams({
            grant_type: 'password', client_id: 'admin-cli',
            username: env.KEYCLOAK_BOOTSTRAP_ADMIN_USERNAME, password: env.KEYCLOAK_BOOTSTRAP_ADMIN_PASSWORD
        })
    });
    exigir(respuesta.status === 200, 'Keycloak rechazó la cuenta administrativa de .env (HTTP ' + respuesta.status + ').');
    const { access_token: token } = await respuesta.json();
    return (ruta, metodo = 'GET', datos) => fetch(base + '/admin/realms' + ruta, {
        method: metodo,
        signal: AbortSignal.timeout(30000),
        headers: { Authorization: 'Bearer ' + token, 'Content-Type': 'application/json' },
        ...(datos === undefined ? {} : { body: datos })
    });
}

async function prepararRealm() {
    const solicitar = await keycloak(leerEnv());
    const existente = await solicitar('/' + realm.realm);
    if (existente.status === 404) {
        const creado = await solicitar('', 'POST', readFileSync(join(__dirname, 'keycloak', 'autotaller-realm.json')));
        exigir(creado.status === 201, 'No se pudo importar el realm (HTTP ' + creado.status + ').');
        console.log('Realm ' + realm.realm + ' importado.');
    } else {
        exigir(existente.status === 200, 'No se pudo consultar el realm (HTTP ' + existente.status + ').');
        console.log('El realm ' + realm.realm + ' ya existe; se conserva.');
    }
}

async function guardarSecretos() {
    const solicitar = await keycloak(leerEnv());
    let cambios = 0;
    for (const [clientId, variable] of Object.entries(secretosClientes)) {
        const [cliente] = await (await solicitar('/' + realm.realm + '/clients?clientId=' + clientId)).json();
        exigir(cliente, 'No existe el cliente ' + clientId + ' en el realm.');
        const { value } = await (await solicitar('/' + realm.realm + '/clients/' + cliente.id + '/client-secret')).json();
        exigir(value, 'Keycloak no devolvió el secreto de ' + clientId + '.');
        if (leerEnv()[variable] !== value) {
            guardarEnv(variable, value);
            cambios++;
        }
    }
    console.log(cambios ? 'Secretos de Keycloak guardados en .env: ' + cambios + '.' : 'Los secretos de .env ya coinciden con Keycloak.');
}

function aplicarEsquemas() {
    for (const { modulo, servicio, cliente, archivos } of esquemas) {
        for (const archivo of archivos) {
            enContenedor(servicio, cliente, { entrada: readFileSync(archivo), capturar: true });
        }
        console.log(modulo + ': estructura lista (' + archivos.length + ' script' + (archivos.length > 1 ? 's' : '') + ').');
    }
}

function aplicarDatosDemo() {
    for (const { modulo, servicio, cliente, archivo } of datosDemo) {
        enContenedor(servicio, cliente, { entrada: readFileSync(archivo), capturar: true });
        console.log(modulo + ': datos de ejemplo listos (' + archivo + ').');
    }
}

function primerAdministrador(opciones) {
    const consulta = enContenedor('mysql-usuarios', clienteMysql + ' -N -e "SELECT COUNT(*) FROM FichasAdministrativas"',
        { capturar: true });
    if (Number(consulta.stdout.trim()) > 0) {
        console.log('Usuarios ya tiene cuentas; no se crea otro administrador.');
        return;
    }

    const clave = opciones.clave ?? randomUUID();
    const datos = JSON.stringify({ clave, email: opciones.admin, nombre: opciones.nombre, apellido: opciones.apellido });
    const resultado = compose(['exec', '-T', 'ms-usuarios', 'dotnet', 'AutoTallerPro.Usuarios.Api.dll',
        '--aprovisionar-primer-administrador'], { entrada: datos, capturar: true, permitirFallo: true });
    if (resultado.status !== 0) {
        console.error(resultado.stderr.trim());
        throw new ErrorEntorno('No se completó el primer administrador. Repite con la misma clave: '
            + 'node infra/levantar-entorno.cjs --admin ' + opciones.admin + ' --clave ' + clave);
    }
    const creado = JSON.parse(resultado.stdout.trim().split(/\r?\n/).pop());
    console.log('Primer administrador creado: ' + creado.username + ' (' + opciones.admin + ').');
    console.log('Abre la invitación en Mailpit (http://127.0.0.1:' + (leerEnv().MAILPIT_WEB_PORT || 8025)
        + '), verifica el correo y elige la contraseña.');
}

async function principal() {
    const opciones = leerArgumentos();
    const [mayor] = process.versions.node.split('.').map(Number);
    exigir(mayor >= 22, 'Se necesita Node.js 22 o posterior.');
    exigir(ejecutar('docker', ['info'], { capturar: true, permitirFallo: true }).status === 0,
        'Docker no responde. Abre Docker Desktop y espera a que diga que está en ejecución.');

    paso('1/7 Archivo .env');
    prepararEnv();

    paso('2/7 Contenedores (compila y arranca; la primera vez tarda varios minutos)');
    compose(['up', '-d', '--build', '--wait', '--wait-timeout', '300']);

    paso('3/7 Realm de Keycloak');
    await prepararRealm();

    paso('4/7 Cuenta técnica de Usuarios, tema e idioma');
    ejecutar(process.execPath, ['infra/keycloak/configurar-usuarios-admin.cjs']);
    ejecutar(process.execPath, ['infra/keycloak/configurar-usuarios-admin.cjs', '--tema']);
    await guardarSecretos();

    paso('5/7 Estructura de las bases' + (opciones.demo ? ' y datos de ejemplo' : ' (sin datos)'));
    aplicarEsquemas();
    if (opciones.demo) aplicarDatosDemo();

    paso('6/7 Aplicaciones con los secretos actualizados');
    compose(['up', '-d', '--wait', '--wait-timeout', '300']);
    compose(['exec', '-T', 'ms-usuarios', 'dotnet', 'AutoTallerPro.Usuarios.Api.dll', '--verificar-mysql']);
    compose(['exec', '-T', 'ms-usuarios', 'dotnet', 'AutoTallerPro.Usuarios.Api.dll', '--verificar-keycloak-admin']);

    paso('7/7 Primer administrador');
    primerAdministrador(opciones);
    ejecutar(process.execPath, ['infra/keycloak/configurar-usuarios-admin.cjs', '--smtp', 'estado']);

    const env = leerEnv();
    console.log('\nEntorno listo.');
    console.log('  Aplicación: http://127.0.0.1:5105  (usa 127.0.0.1, no localhost)');
    console.log('  Correo de prueba (Mailpit): http://127.0.0.1:' + (env.MAILPIT_WEB_PORT || 8025));
    console.log('  Consola de Keycloak: http://127.0.0.1:' + (env.KEYCLOAK_PORT || 8180) + '/admin/');
    console.log('Bases para DBeaver o Compass (host 127.0.0.1; usuario y contraseña en .env):');
    console.log('  PostgreSQL Clientes/Vehículos: puerto ' + (env.POSTGRES_CLIENTES_PORT || 5433) + ', base ' + env.POSTGRES_CLIENTES_DB);
    console.log('  MySQL Servicios: puerto ' + (env.MYSQL_SERVICIOS_PORT || 3307) + ', base ' + env.MYSQL_SERVICIOS_DB);
    console.log('  MySQL Usuarios: puerto ' + (env.MYSQL_USUARIOS_PORT || 3308) + ', base ' + env.MYSQL_USUARIOS_DB);
    console.log('  MongoDB Mecánicos: puerto ' + (env.MONGO_MECANICOS_PORT || 27018) + ', base ' + env.MONGO_MECANICOS_DB
        + ' (authSource ' + env.MONGO_MECANICOS_DB + ')');
    console.log('Para detenerlo sin perder datos: docker compose stop');
}

principal().catch(error => {
    console.error('\n' + (error instanceof ErrorEntorno ? error.message : 'Error inesperado: ' + error.message));
    process.exitCode = 1;
});
