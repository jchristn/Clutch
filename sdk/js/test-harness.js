'use strict';

/**
 * Clutch JavaScript SDK test harness.
 *
 * Non-interactive: exercises the admin (REST) and lock (WebSocket) clients, asserts expected
 * behavior, and exits with code 0 on success or 1 on failure.
 *
 * Usage: node test-harness.js <endpoint> <accessKey> [secretKey]
 */

const { ClutchAdminClient, LockMode, LockBehavior } = require('./clutch-admin-sdk');
const { ClutchLockClient, ClutchLockDeniedError, AcquireResult } = require('./clutch-lock-sdk');

let passed = 0;
let failed = 0;

async function check(name, fn) {
    try {
        await fn();
        passed++;
        console.log(`[PASS] ${name}`);
    } catch (err) {
        failed++;
        console.log(`[FAIL] ${name}`);
        console.log(`       ${err.name || 'Error'}: ${err.message}`);
    }
}

function assert(condition, message) {
    if (!condition) throw new Error(message);
}

async function assertThrows(ErrorType, fn) {
    try {
        await fn();
    } catch (err) {
        if (err instanceof ErrorType) return err;
        throw new Error(`Expected ${ErrorType.name} but caught ${err.name}: ${err.message}`);
    }
    throw new Error(`Expected ${ErrorType.name} but no error was thrown.`);
}

function uniqueKey(prefix) {
    return `${prefix}/${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
}

async function main() {
    const endpoint = process.argv[2] || 'http://127.0.0.1:8090';
    const accessKey = process.argv[3] || 'clutch-default-access-key';
    const secretKey = process.argv[4] || null;

    console.log('========================================');
    console.log('  Clutch JavaScript SDK Test Harness');
    console.log('========================================');
    console.log(`Endpoint : ${endpoint}`);
    console.log(`AccessKey: ${accessKey}`);
    console.log('');

    const admin = new ClutchAdminClient(endpoint);
    let tenantId = null;

    await check('Health check is healthy', async () => {
        const health = await admin.getHealth();
        assert(!!health.status, 'status should be present');
    });

    await check('Authenticate with application key', async () => {
        const token = await admin.authenticateWithKey(accessKey, secretKey || accessKey);
        assert(!!token.token, 'token should be returned');
        assert(!!token.tenantId, 'tenantId should be returned');
        tenantId = token.tenantId;
    });

    await check('Get token details', async () => {
        const details = await admin.getTokenDetails();
        assert(details.authenticated === true, 'principal should be authenticated');
        assert(!!details.tenantId, 'tenantId should be present');
    });

    await check('Get server info', async () => {
        const info = await admin.getServerInfo();
        assert((info.product || '').toLowerCase() === 'clutch', `product should be Clutch, was '${info.product}'`);
    });

    await check('List tenants includes at least one tenant', async () => {
        const tenants = await admin.listTenants();
        assert(Array.isArray(tenants) && tenants.length >= 1, 'expected at least one tenant');
    });

    await check('Create, read, and delete a tenant', async () => {
        const name = 'sdk-test-' + Math.random().toString(36).slice(2, 12);
        const created = await admin.createTenant(name, { lockHistoryRetentionDays: 7, defaultLeaseMs: 30000, maxLeaseMs: 300000 });
        assert(!!created.id, 'created tenant should have an id');
        const fetched = await admin.getTenant(created.id);
        assert(fetched.name === name, 'fetched tenant name should match');
        await admin.deleteTenant(created.id);
    });

    await check('Create, list, and delete a credential', async () => {
        const created = await admin.createCredential(tenantId, 'sdk-test-cred-' + Math.random().toString(36).slice(2, 8));
        assert(!!created.id, 'created credential should have an id');
        assert(!!created.accessKey, 'created credential should return an access key');
        assert(!!created.secretKey, 'created credential should return the raw secret key once');
        const credentials = await admin.listCredentials(tenantId);
        assert(credentials.some((c) => c.id === created.id), 'created credential should appear in the list');
        await admin.deleteCredential(tenantId, created.id);
    });

    await check('List active locks (REST observability)', async () => {
        const holders = await admin.listLocks(tenantId);
        assert(Array.isArray(holders), 'locks listing should be an array');
    });

    await check('Read request-history summary', async () => {
        const summary = await admin.getRequestHistorySummary({ bucketMinutes: 60 });
        assert(summary && summary.totalCount >= 0, 'total count should be non-negative');
    });

    // ---- Lock (WebSocket) ----

    const locks = new ClutchLockClient(endpoint, accessKey, secretKey);

    await check('Connect lock client and receive welcome', async () => {
        const welcome = await locks.connect();
        assert(!!welcome.sessionId, 'welcome should include a session id');
        assert(welcome.heartbeatIntervalMs > 0, 'welcome should include a heartbeat interval');
    });

    const writeKey = uniqueKey('sdk-test');
    let firstToken = 0;

    await check('Acquire and release a write lock; obtain a fencing token', async () => {
        const acquired = await locks.acquire(writeKey, LockMode.Write);
        assert(!!acquired.holderId, 'acquired lock should have a holder id');
        assert(acquired.fencingToken >= 1, 'fencing token should be at least 1');
        assert(acquired.mode === LockMode.Write, 'mode should be Write');
        firstToken = acquired.fencingToken;
        const released = await locks.release(acquired.holderId);
        assert(released === true, 'release should report true');
    });

    await check('Fencing token increases on re-acquire of the same key', async () => {
        const acquired = await locks.acquire(writeKey, LockMode.Write);
        assert(acquired.fencingToken > firstToken, `fencing token should increase (was ${firstToken}, got ${acquired.fencingToken})`);
        await locks.release(acquired.holderId);
    });

    await check('Multiple readers may share a key', async () => {
        const readKey = uniqueKey('sdk-test-read');
        const r1 = await locks.acquire(readKey, LockMode.Read);
        const r2 = await locks.acquire(readKey, LockMode.Read);
        assert(!!r1.holderId && !!r2.holderId, 'both read holders should be granted');
        assert(r1.holderId !== r2.holderId, 'two read holders should be distinct');
        await locks.release(r1.holderId);
        await locks.release(r2.holderId);
    });

    await check('A write lock denies a fail-fast read on the same key', async () => {
        const key = uniqueKey('sdk-test-conflict');
        const w = await locks.acquire(key, LockMode.Write);
        try {
            const denied = await assertThrows(ClutchLockDeniedError, () => locks.acquire(key, LockMode.Read, { behavior: LockBehavior.FailFast }));
            assert(denied.result === AcquireResult.Denied, `result should be Denied, was ${denied.result}`);
        } finally {
            await locks.release(w.holderId);
        }
    });

    await check('A waiting read times out while a write is held', async () => {
        const key = uniqueKey('sdk-test-timeout');
        const w = await locks.acquire(key, LockMode.Write);
        try {
            const denied = await assertThrows(ClutchLockDeniedError, () => locks.acquire(key, LockMode.Read, { behavior: LockBehavior.Wait, timeoutMs: 500 }));
            assert(denied.result === AcquireResult.Timeout, `result should be Timeout, was ${denied.result}`);
        } finally {
            await locks.release(w.holderId);
        }
    });

    await check('Heartbeat renews a held lease', async () => {
        const key = uniqueKey('sdk-test-hb');
        const acquired = await locks.acquire(key, LockMode.Write);
        try {
            const renewed = new Promise((resolve) => locks.once('heartbeat', () => resolve(true)));
            await locks.heartbeat([acquired.holderId]);
            const winner = await Promise.race([renewed, new Promise((resolve) => setTimeout(() => resolve(false), 3000))]);
            assert(winner === true, 'expected a heartbeat renewal frame within 3 seconds');
        } finally {
            await locks.release(acquired.holderId);
        }
    });

    await check('Close the lock connection', async () => {
        await locks.close();
    });

    await check('Logout', async () => {
        await admin.logout();
    });

    console.log('');
    console.log('========================================');
    console.log(`  Passed: ${passed}   Failed: ${failed}`);
    console.log('========================================');
    process.exit(failed === 0 ? 0 : 1);
}

main().catch((err) => {
    console.error(`FATAL: ${err.name || 'Error'}: ${err.message}`);
    process.exit(1);
});
