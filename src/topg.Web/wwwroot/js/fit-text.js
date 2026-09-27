// Shrinks the text of every [data-fit-text] box until it fits, down to a minimum size. CSS can't derive a font size
// from the box area and the text length, and word wrapping makes any formula approximate, so this measures instead.
// Blazor inserts and removes the boxes when hints are revealed, so new boxes are picked up by a MutationObserver.
(() => {
    const minSize = 11;
    const observed = new WeakSet();
    const resizes = new ResizeObserver(entries => entries.forEach(entry => fit(entry.target)));

    function fits(box, text) {
        const style = getComputedStyle(box);
        const width = box.clientWidth - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight);
        const height = box.clientHeight - parseFloat(style.paddingTop) - parseFloat(style.paddingBottom);
        return text.scrollWidth <= width && text.scrollHeight <= height;
    }

    function fit(box) {
        const text = box.firstElementChild;
        if (!text) {
            return;
        }

        let low = minSize;
        let high = Number(box.dataset.fitText) || 34;
        while (low < high) {
            const size = Math.ceil((low + high) / 2);
            text.style.fontSize = `${size}px`;
            if (fits(box, text)) {
                low = size;
            } else {
                high = size - 1;
            }
        }

        text.style.fontSize = `${low}px`;
    }

    function scan(node) {
        if (!(node instanceof Element)) {
            return;
        }

        const boxes = node.matches("[data-fit-text]") ? [node] : node.querySelectorAll("[data-fit-text]");
        for (const box of boxes) {
            if (!observed.has(box)) {
                observed.add(box);
                resizes.observe(box);
            }

            fit(box);
        }
    }

    new MutationObserver(mutations => {
        for (const mutation of mutations) {
            mutation.addedNodes.forEach(scan);
            // A reused box whose text changed has to be measured again.
            if (mutation.type === "characterData") {
                const box = mutation.target.parentElement?.closest("[data-fit-text]");
                if (box) {
                    fit(box);
                }
            }
        }
    }).observe(document.documentElement, { childList: true, subtree: true, characterData: true });
})();
