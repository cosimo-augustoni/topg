const DB_NAME = "topg-creator";
const DB_VERSION = 1;

let dbPromise = null;

function openDb() {
    if (dbPromise) {
        return dbPromise;
    }

    dbPromise = new Promise((resolve, reject) => {
        if (!("indexedDB" in globalThis)) {
            reject(new Error("IndexedDB is not available in this browser."));
            return;
        }

        const request = indexedDB.open(DB_NAME, DB_VERSION);
        request.onupgradeneeded = () => {
            const db = request.result;
            if (!db.objectStoreNames.contains("projects")) {
                db.createObjectStore("projects", { keyPath: "id" });
            }
            if (!db.objectStoreNames.contains("images")) {
                db.createObjectStore("images", { keyPath: "hash" });
            }
            if (!db.objectStoreNames.contains("settings")) {
                db.createObjectStore("settings", { keyPath: "key" });
            }
        };
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
        request.onblocked = () => reject(new Error("The creator database is blocked by another open tab."));
    });

    // Allow a retry after a failed open.
    dbPromise.catch(() => { dbPromise = null; });
    return dbPromise;
}

function promisify(request) {
    return new Promise((resolve, reject) => {
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

async function run(storeName, mode, action) {
    const db = await openDb();
    const tx = db.transaction(storeName, mode);
    const done = new Promise((resolve, reject) => {
        tx.oncomplete = () => resolve();
        tx.onerror = () => reject(tx.error);
        tx.onabort = () => reject(tx.error ?? new Error("Transaction aborted."));
    });
    const result = await action(tx.objectStore(storeName));
    await done;
    return result;
}

function imageMetadata(record) {
    const { blob, ...metadata } = record;
    return metadata;
}

export function listProjects() {
    return run("projects", "readonly", async store => (await promisify(store.getAll())).map(r => r.json));
}

export function getProject(id) {
    return run("projects", "readonly", async store => (await promisify(store.get(id)))?.json ?? null);
}

export function saveProject(id, json) {
    return run("projects", "readwrite", store => promisify(store.put({ id, json })));
}

export function deleteProject(id) {
    return run("projects", "readwrite", store => promisify(store.delete(id)));
}

async function measure(blob) {
    try {
        const bitmap = await createImageBitmap(blob);
        const size = { width: bitmap.width, height: bitmap.height };
        bitmap.close();
        return size;
    } catch {
        return { width: null, height: null };
    }
}

export async function putImage(hash, extension, contentType, bytes) {
    const existing = await getImageMetadata(hash);
    if (existing) {
        return existing;
    }

    const blob = new Blob([bytes], { type: contentType });
    const { width, height } = await measure(blob);
    const record = {
        hash,
        extension,
        contentType,
        size: blob.size,
        width,
        height,
        createdAt: new Date().toISOString(),
        blob,
    };
    await run("images", "readwrite", store => promisify(store.put(record)));
    return imageMetadata(record);
}

export function getImageMetadata(hash) {
    return run("images", "readonly", async store => {
        const record = await promisify(store.get(hash));
        return record ? imageMetadata(record) : null;
    });
}

export function listImages() {
    return run("images", "readonly", async store => (await promisify(store.getAll())).map(imageMetadata));
}

export async function getImageBytes(hash) {
    const record = await run("images", "readonly", store => promisify(store.get(hash)));
    return record ? new Uint8Array(await record.blob.arrayBuffer()) : null;
}

export function deleteImages(hashes) {
    return run("images", "readwrite", async store => {
        for (const hash of hashes) {
            await promisify(store.delete(hash));
        }
    });
}

export async function createObjectUrl(hash) {
    const record = await run("images", "readonly", store => promisify(store.get(hash)));
    return record ? URL.createObjectURL(record.blob) : null;
}

export function revokeObjectUrl(url) {
    URL.revokeObjectURL(url);
}

export function getSetting(key) {
    return run("settings", "readonly", async store => (await promisify(store.get(key)))?.value ?? null);
}

export function setSetting(key, value) {
    return run("settings", "readwrite", store => promisify(store.put({ key, value })));
}

export async function getStorageEstimate() {
    const storage = navigator.storage;
    const estimate = storage?.estimate ? await storage.estimate() : {};
    const persisted = storage?.persisted ? await storage.persisted() : false;
    return {
        usage: estimate.usage ?? null,
        quota: estimate.quota ?? null,
        persisted,
        canPersist: !!storage?.persist,
    };
}

export async function requestPersistence() {
    return navigator.storage?.persist ? await navigator.storage.persist() : false;
}
