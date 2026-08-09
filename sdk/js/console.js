'use strict';

/**
 * Clutch JavaScript SDK interactive console.
 *
 * A readline REPL for driving the SDK by hand: authenticate, inspect tenants and locks, connect the
 * lock client, and acquire, release, and heartbeat locks while watching server pushes.
 *
 * Usage: node console.js [endpoint]   (or: npm run console)
 */

const readline = require('readline');
const { ClutchAdminClient } = require('./clutch-admin-sdk');
const { ClutchLockClient, ClutchLockDeniedError } = require('./clutch-lock-sdk');

const endpoint = process.argv[2] || 'http://127.0.0.1:8090';
const admin = new ClutchAdminClient(endpoint);
let locks = null;
let tenantId = null;
const held = new Map();
let holderCounter = 0;

const rl = readline.createInterface({ input: process.stdin, output: process.stdout, prompt: 'clutch> ' });

function printHelp() {
    console.log('Commands:');
    console.log('  login key <accessKey>                 Authenticate with an application key');
    console.log('  login user <tenantId> <email> <pw>    Authenticate with user credentials');
    console.log('  whoami                                Show the current principal');
    console.log('  serverinfo                            Show server information');
    console.log('  tenants                               List tenants');
    console.log('  locks                                 List active locks in the current tenant');
    console.log('  connect <accessKey>                   Open the WebSocket lock connection');
    console.log('  acquire <key> <Read|Write|Delete> [wait <ms>]   Acquire a lock');
    console.log('  held                                  List locks held on this connection');
    console.log('  release <n>                           Release a held lock by its list number');
    console.log('  heartbeat                             Send a heartbeat for all held locks');
    console.log('  logout                                Revoke the current session');
    console.log('  quit                                  Exit');
}

async function handle(line) {
    const parts = line.trim().split(/\s+/).filter((p) => p.length > 0);
    if (parts.length === 0) return;
    const command = parts[0].toLowerCase();

    switch (command) {
        case 'help':
            printHelp();
            break;
        case 'login': {
            if (parts[1] === 'key' && parts.length >= 3) {
                const token = await admin.authenticateWithKey(parts[2]);
                tenantId = token.tenantId;
                console.log(`Authenticated. Tenant: ${token.tenantId}`);
            } else if (parts[1] === 'user' && parts.length >= 5) {
                const token = await admin.authenticateWithPassword(parts[2], parts[3], parts[4]);
                tenantId = token.tenantId;
                console.log(`Authenticated. Tenant: ${token.tenantId}`);
            } else {
                console.log('Usage: login key <accessKey>  |  login user <tenantId> <email> <password>');
            }
            break;
        }
        case 'whoami': {
            const d = await admin.getTokenDetails();
            console.log(`Principal : ${d.principalName}`);
            console.log(`Type      : ${d.principalType}`);
            console.log(`Tenant    : ${d.tenantId}`);
            console.log(`IsAdmin   : ${d.isAdmin}`);
            console.log(`TenantAdmin: ${d.isTenantAdmin}`);
            break;
        }
        case 'serverinfo': {
            const info = await admin.getServerInfo();
            console.log(`Product : ${info.product} ${info.version}`);
            console.log(`Node    : ${info.node}`);
            console.log(`Database: ${info.database}`);
            console.log(`WS conns: ${info.webSocketConnections}`);
            break;
        }
        case 'tenants': {
            const tenants = await admin.listTenants();
            console.log(`${tenants.length} tenant(s):`);
            for (const t of tenants) console.log(`  ${t.id}  ${t.name}  (active=${t.active}, protected=${t.isProtected})`);
            break;
        }
        case 'locks': {
            if (!tenantId) { console.log('Not authenticated. Use "login" first.'); break; }
            const holders = await admin.listLocks(tenantId);
            console.log(`${holders.length} active holder(s):`);
            for (const h of holders) console.log(`  ${h.lockKey}  ${h.mode}  fencing=${h.fencingToken}  holder=${h.id}`);
            break;
        }
        case 'connect': {
            if (parts.length < 2) { console.log('Usage: connect <accessKey>'); break; }
            if (locks) await locks.close();
            locks = new ClutchLockClient(endpoint, parts[1]);
            locks.on('heartbeat', (renewed) => console.log(`\n[push] heartbeat renewed ${renewed.length} lease(s)`));
            locks.on('error', (message) => console.log(`\n[push] error: ${message}`));
            locks.on('close', (reason) => console.log(`\n[push] connection closed: ${reason}`));
            const welcome = await locks.connect();
            console.log(`Connected. Session ${welcome.sessionId}, heartbeat every ${welcome.heartbeatIntervalMs} ms.`);
            break;
        }
        case 'acquire': {
            if (!locks) { console.log('Not connected. Use "connect" first.'); break; }
            if (parts.length < 3) { console.log('Usage: acquire <key> <Read|Write|Delete> [wait <ms>]'); break; }
            const options = {};
            if (parts[3] === 'wait' && parts[4]) { options.behavior = 'Wait'; options.timeoutMs = parseInt(parts[4], 10); }
            const acquired = await locks.acquire(parts[1], parts[2], options);
            const n = ++holderCounter;
            held.set(n, acquired);
            console.log(`Acquired [${n}] key=${acquired.key} mode=${acquired.mode} fencing=${acquired.fencingToken} lease=${acquired.leaseExpiresUtc}`);
            break;
        }
        case 'held': {
            if (held.size === 0) { console.log('No locks held on this connection.'); break; }
            for (const [n, h] of held) console.log(`  [${n}] key=${h.key} mode=${h.mode} fencing=${h.fencingToken} holder=${h.holderId}`);
            break;
        }
        case 'release': {
            if (!locks) { console.log('Not connected. Use "connect" first.'); break; }
            const n = parseInt(parts[1], 10);
            const h = held.get(n);
            if (!h) { console.log('Usage: release <n>  (see "held" for numbers)'); break; }
            const released = await locks.release(h.holderId);
            held.delete(n);
            console.log(`Released [${n}]: ${released}`);
            break;
        }
        case 'heartbeat': {
            if (!locks) { console.log('Not connected. Use "connect" first.'); break; }
            await locks.heartbeat();
            console.log('Heartbeat sent for all held locks.');
            break;
        }
        case 'logout': {
            await admin.logout();
            tenantId = null;
            console.log('Logged out.');
            break;
        }
        case 'quit':
        case 'exit':
            rl.close();
            return;
        default:
            console.log(`Unknown command '${command}'. Type 'help'.`);
    }
}

console.log('');
console.log('Clutch interactive console. Type "help" for commands, "quit" to exit.');
console.log(`Endpoint: ${endpoint}`);
console.log('');
rl.prompt();

rl.on('line', async (line) => {
    try {
        await handle(line);
    } catch (err) {
        if (err instanceof ClutchLockDeniedError) console.log(`Lock denied (${err.result}): ${err.message}`);
        else console.log(`Error: ${err.message}`);
    }
    rl.prompt();
});

rl.on('close', async () => {
    if (locks) await locks.close();
    console.log('Goodbye.');
    process.exit(0);
});
