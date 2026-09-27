function isTextInput(element) {
    return element instanceof HTMLElement
        && (element.isContentEditable || ["INPUT", "TEXTAREA", "SELECT"].includes(element.tagName));
}

function isInDialog(element) {
    return element instanceof Element && element.closest(".mud-dialog-container, .mud-popover") !== null;
}

function toShortcut(e) {
    const ctrl = e.ctrlKey || e.metaKey;
    const key = e.key.toLowerCase();

    if (ctrl && !e.altKey && !e.shiftKey) {
        if (key === "s") return "save";
        if (key === "e") return "export";
        // Inside text fields Ctrl+Z is the field's own text undo.
        if (key === "z" && !isTextInput(e.target)) return "undo";
    }

    if (e.altKey && !ctrl) {
        switch (e.key) {
            case "ArrowUp": return "up";
            case "ArrowDown": return "down";
            case "ArrowLeft": return "left";
            case "ArrowRight": return "right";
        }
    }

    if (e.key === "Escape" && !ctrl && !e.altKey) {
        // Let open menus handle Escape themselves (dialogs are handled in registerShortcuts).
        if (!document.querySelector(".mud-popover-open")) return "escape";
    }

    return null;
}

export function registerShortcuts(dotNetRef) {
    const handler = e => {
        if (e.isComposing || e.repeat && e.key === "Escape") {
            return;
        }

        const shortcut = toShortcut(e);
        if (!shortcut) {
            return;
        }

        // While a dialog is open the editor behind it must not change; the dialog handles Escape itself.
        // In WASM the dialog may already be closed (removed) by the time this runs, so the target counts too.
        if (isInDialog(e.target) || document.querySelector(".mud-dialog-container")) {
            // Still keep the browser's "save page" dialog away.
            if (shortcut === "save") e.preventDefault();
            return;
        }

        // Alt+← / Alt+→ would otherwise navigate the browser history.
        e.preventDefault();
        dotNetRef.invokeMethodAsync("OnShortcut", shortcut);
    };

    document.addEventListener("keydown", handler);
    return { dispose: () => document.removeEventListener("keydown", handler) };
}

export function scrollIntoView(selector) {
    document.querySelector(selector)?.scrollIntoView({ block: "nearest", inline: "nearest" });
}
