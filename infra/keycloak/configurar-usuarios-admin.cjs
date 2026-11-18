const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const { parseEnv } = require('node:util');

const realm = JSON.parse(readFileSync(join(__dirname, 'autotaller-realm.json'), 'utf8'));
const definicion = realm.clients.find(client => client.clientId === 'autotaller-usuarios-api');
const socio = 'p43-cuenta-tecnica';

function exigir(condicion, mensaje) {
    if (!condicion) throw new Error(mensaje);
}

function iguales(a = [], b = []) {
    return JSON.stringify([...a].sort()) === JSON.stringify([...b].sort());
}

async function configurar() {
    const local = process.env.KEYCLOAK_BOOTSTRAP_ADMIN_USERNAME && process.env.KEYCLOAK_BOOTSTRAP_ADMIN_PASSWORD
        ? process.env
        : parseEnv(readFileSync('.env', 'utf8'));
    const url = new URL(process.argv[2] ?? ('http://127.0.0.1:' + (local.KEYCLOAK_PORT || 8180)));
    exigir(!url.username && !url.password && !url.search && !url.hash &&
        (url.protocol === 'https:' || (url.protocol === 'http:' && ['127.0.0.1', 'localhost', '[::1]'].includes(url.hostname))),
        'Usa HTTPS o HTTP en loopback.');
    const base = url.href.replace(/\/+$/, '');
    const tokenResponse = await fetch(base + '/realms/master/protocol/openid-connect/token', {
        method: 'POST',
        redirect: 'error',
        signal: AbortSignal.timeout(20000),
        body: new URLSearchParams({
            grant_type: 'password', client_id: 'admin-cli',
            username: local.KEYCLOAK_BOOTSTRAP_ADMIN_USERNAME, password: local.KEYCLOAK_BOOTSTRAP_ADMIN_PASSWORD
        })
    });
    exigir(tokenResponse.status === 200, 'Autenticación administrativa HTTP ' + tokenResponse.status);
    const { access_token: token } = await tokenResponse.json();
    const raiz = base + '/admin/realms/' + realm.realm;

    async function solicitar(ruta, metodo = 'GET', datos, esperados = [200]) {
        const respuesta = await fetch(raiz + ruta, {
            method: metodo, redirect: 'error', signal: AbortSignal.timeout(20000),
            headers: { Authorization: 'Bearer ' + token, 'Content-Type': 'application/json' },
            ...(datos === undefined ? {} : { body: JSON.stringify(datos) })
        });
        exigir(esperados.includes(respuesta.status), metodo + ' ' + ruta + ' devolvió HTTP ' + respuesta.status);
        return respuesta;
    }
    const leer = async ruta => (await solicitar(ruta)).json();
    const buscarCliente = async id => {
        const clientes = await leer('/clients?clientId=' + id);
        exigir(clientes.length < 2, 'Cliente ambiguo: ' + id);
        return clientes[0];
    };
    const crear = (ruta, datos) => solicitar(ruta, 'POST', datos, [201]);
    const escribir = (ruta, metodo, datos) => solicitar(ruta, metodo, datos, [204]);

    if (!(await leer('')).adminPermissionsEnabled) {
        await escribir('', 'PUT', { adminPermissionsEnabled: true });
    }
    let cliente = await buscarCliente(definicion.clientId);
    if (!cliente) {
        await crear('/clients', definicion);
        cliente = await buscarCliente(definicion.clientId);
    }
    for (const campo of ['enabled', 'protocol', 'publicClient', 'clientAuthenticatorType', 'standardFlowEnabled',
        'implicitFlowEnabled', 'directAccessGrantsEnabled', 'serviceAccountsEnabled', 'fullScopeAllowed']) {
        exigir(cliente[campo] === definicion[campo], 'Revisa el campo existente ' + campo + ' del cliente técnico.');
    }
    exigir(!cliente.redirectUris?.length && !cliente.webOrigins?.length, 'El cliente tiene redirecciones u orígenes web.');
    const rutaCliente = '/clients/' + cliente.id;
    const { defaultClientScopes: todos } = definicion;
    const [defaults, opcionales] = await Promise.all([
        leer(rutaCliente + '/default-client-scopes'), leer(rutaCliente + '/optional-client-scopes')
    ]);
    for (const scope of defaults.filter(scope => !todos.includes(scope.name))) {
        await escribir(rutaCliente + '/default-client-scopes/' + scope.id, 'DELETE');
    }
    for (const scope of opcionales) await escribir(rutaCliente + '/optional-client-scopes/' + scope.id, 'DELETE');
    const scopes = await leer('/client-scopes');
    for (const nombre of todos.filter(scope => !defaults.some(actual => actual.name === scope))) {
        const scope = scopes.find(scope => scope.name === nombre);
        exigir(scope, 'No se encontró el scope nativo ' + nombre);
        await escribir(rutaCliente + '/default-client-scopes/' + scope.id, 'PUT');
    }

    const cuenta = await leer(rutaCliente + '/service-account-user');
    const gestion = await buscarCliente('realm-management');
    const rutaGestion = '/clients/' + gestion.id;
    const query = await leer(rutaGestion + '/roles/query-users');
    const rutaCuenta = '/users/' + cuenta.id + '/role-mappings';
    for (const ruta of [rutaCuenta, rutaCliente + '/scope-mappings']) {
        const mapeos = await leer(ruta);
        exigir((mapeos.realmMappings ?? []).every(rol => rol.name === 'default-roles-' + realm.realm)
            && Object.entries(mapeos.clientMappings ?? {}).every(([id, mapeo]) =>
                id === 'realm-management' && mapeo.mappings.every(rol => rol.id === query.id)),
        'Hay roles adicionales en la cuenta técnica o en su alcance.');
    }
    const efectivos = await leer(rutaCuenta + '/realm/composite');
    exigir(!efectivos.some(rol => ['Administrador', 'Recepcionista'].includes(rol.name)),
        'La cuenta técnica tiene roles de negocio efectivos.');
    for (const ruta of [rutaCuenta + '/clients/' + gestion.id, rutaCliente + '/scope-mappings/clients/' + gestion.id]) {
        if (!(await leer(ruta)).some(rol => rol.id === query.id)) await escribir(ruta, 'POST', [query]);
    }

    const grupos = {};
    const roles = {};
    for (const grupo of realm.groups) {
        const ruta = '/group-by-path/' + grupo.name;
        let respuesta = await solicitar(ruta, 'GET', undefined, [200, 404]);
        const nombreRol = grupo.realmRoles?.[0];
        const rol = nombreRol ? await leer('/roles/' + nombreRol) : null;
        exigir(!rol || (!rol.clientRole && !rol.composite), 'El rol de negocio debe ser directo y de realm.');
        if (respuesta.status === 404) {
            await crear('/groups', { name: grupo.name });
            respuesta = await solicitar(ruta);
            const nuevo = await respuesta.clone().json();
            if (rol) await escribir('/groups/' + nuevo.id + '/role-mappings/realm', 'POST', [rol]);
        }
        const remoto = await respuesta.json();
        const mapeos = await leer('/groups/' + remoto.id + '/role-mappings');
        exigir(remoto.path === '/' + grupo.name && !remoto.subGroupCount
            && !Object.keys(mapeos.clientMappings ?? {}).length
            && iguales((mapeos.realmMappings ?? []).map(rol => rol.id), rol ? [rol.id] : []),
        'Revisa el grupo existente ' + grupo.name + ' y sus roles.');
        grupos[grupo.name] = remoto.id;
        if (rol) roles[rol.name] = rol.id;
    }

    const permisosCliente = await buscarCliente('admin-permissions');
    const authz = '/clients/' + permisosCliente.id + '/authz/resource-server';
    async function asegurar(tipo, definicion) {
        const ruta = authz + '/' + tipo;
        const resultado = await solicitar(ruta + '/search?name=' + definicion.name, 'GET', undefined, [200, 204, 404]);
        const existente = resultado.status === 200 ? await resultado.json() : null;
        const recurso = existente ?? await (await crear(ruta, definicion)).json();
        return leer(ruta + '/' + recurso.id);
    }

    const politica = await asegurar('policy/user', { name: socio, logic: 'POSITIVE', users: [cuenta.id] });
    exigir(politica.logic === 'POSITIVE' && iguales(politica.users, [cuenta.id]),
        'Revisa la política existente ' + socio + '.');

    for (const permiso of [
        { name: 'p43-gestion', resourceType: 'Groups', resources: [grupos['usuarios-administrados']],
            scopes: ['view', 'view-members', 'manage-members', 'manage-membership', 'manage-membership-of-members'] },
        { name: 'p43-grupos-roles', resourceType: 'Groups',
            resources: [grupos['usuarios-rol-administrador'], grupos['usuarios-rol-recepcionista']],
            scopes: ['view', 'manage-membership'] },
        { name: 'p43-roles', resourceType: 'Roles', resources: Object.values(roles), scopes: ['map-role'] }
    ]) {
        const actual = await asegurar('permission/scope', {
            ...permiso, policies: [socio], decisionStrategy: 'UNANIMOUS'
        });
        const ruta = authz + '/policy/' + actual.id;
        const [recursos, operaciones, politicas] = await Promise.all([
            leer(ruta + '/resources'), leer(ruta + '/scopes'), leer(ruta + '/associatedPolicies')
        ]);
        exigir(actual.resourceType === permiso.resourceType && actual.decisionStrategy === 'UNANIMOUS'
            && iguales(recursos.map(recurso => recurso.name), permiso.resources)
            && iguales(operaciones.map(scope => scope.name), permiso.scopes)
            && iguales(politicas.map(policy => policy.id), [politica.id]),
        'Revisa el permiso existente ' + permiso.name + '.');
    }

    const perfil = await leer('/users/profile');
    const atributoAlta = perfil.attributes.find(campo => campo.name === 'p4AltaId');
    if (!atributoAlta) {
        perfil.attributes.push({
            name: 'p4AltaId', displayName: 'Correlación de alta de Usuarios', multivalued: false,
            permissions: { view: ['admin'], edit: ['admin'] },
            validations: { pattern: { pattern: '^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$' } }
        });
        await solicitar('/users/profile', 'PUT', perfil, [200, 204]);
    } else {
        exigir(iguales(atributoAlta.permissions?.view, ['admin']) && iguales(atributoAlta.permissions?.edit, ['admin'])
            && !atributoAlta.multivalued && !atributoAlta.required,
        'Revisa los permisos del atributo de perfil p4AltaId.');
    }

    console.log('Cuenta técnica, grupos, permisos FGAP y perfil de alta de Usuarios verificados.');
}

configurar().catch(error => {
    console.error(error instanceof Error && !/https?:\/\/|token=|password=/i.test(error.message)
        ? error.message : 'Falló la configuración de Keycloak.');
    process.exitCode = 1;
});
