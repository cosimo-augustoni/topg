// Draws an image onto a canvas that is `blocks` pixels wide; CSS (image-rendering: pixelated) scales it up, so every
// screen shows the same block grid whatever its size. The CDN sends no CORS headers, which taints the canvas: it can be
// displayed, but reading it back (getImageData/toDataURL) would throw.
(() => {
    const images = new Map();
    const requested = new WeakMap();

    function load(url) {
        if (!images.has(url)) {
            images.set(url, new Promise((resolve, reject) => {
                const image = new Image();
                image.onload = () => resolve(image);
                image.onerror = () => {
                    images.delete(url);
                    reject(new Error(`Could not load ${url}`));
                };
                image.src = url;
            }));
        }

        return images.get(url);
    }

    // A single large downscale samples only a few source pixels per block, so coarse steps would look noisy instead of
    // averaged. Halving repeatedly lets every step blend the pixels of the one before.
    function draw(canvas, image, blocks) {
        let source = image;
        let width = image.naturalWidth;
        let height = image.naturalHeight;
        while (width / 2 >= blocks) {
            const half = document.createElement("canvas");
            half.width = Math.ceil(width / 2);
            half.height = Math.ceil(height / 2);
            const context = half.getContext("2d");
            context.imageSmoothingQuality = "high";
            context.drawImage(source, 0, 0, half.width, half.height);
            source = half;
            width = half.width;
            height = half.height;
        }

        canvas.width = blocks;
        canvas.height = Math.max(1, Math.round(blocks * image.naturalHeight / image.naturalWidth));
        const context = canvas.getContext("2d");
        context.imageSmoothingQuality = "high";
        context.drawImage(source, 0, 0, canvas.width, canvas.height);
    }

    window.pixelateImage = async (canvas, url, blocks) => {
        const key = `${blocks}|${url}`;
        requested.set(canvas, key);
        let image;
        try {
            image = await load(url);
        } catch (error) {
            console.warn(error.message);
            return;
        }

        // The host may have moved the slider again while the image was loading.
        if (requested.get(canvas) === key) {
            draw(canvas, image, blocks);
        }
    };
})();
