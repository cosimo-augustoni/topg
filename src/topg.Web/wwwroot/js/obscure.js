// Draws a swirled, optionally blurred copy of an image onto a canvas of a fixed width that CSS scales to the screen, so
// every screen sees the same picture whatever its size. The CDN sends no CORS headers, which taints the canvas: it can
// be displayed, but reading it back (getImageData/toDataURL) would throw. So everything is built from clipped, rotated
// and scaled drawImage calls, never from pixel data.
(() => {
    // Enough rings that neighbouring rings differ by only a small angle, and a canvas that is sharp enough when it is
    // scaled up to a big screen while the swirl stays cheap to draw on phones. Small images are drawn at that width
    // too, since swirling them at their own size would show their pixels.
    const rings = 200;
    const detailWidth = 640;
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

    // A single large downscale samples only a few source pixels per target pixel, which looks noisy instead of
    // blurred. Halving repeatedly lets every step blend the pixels of the one before.
    function scaled(image, width, height) {
        let source = image;
        let sourceWidth = image.naturalWidth ?? image.width;
        let sourceHeight = image.naturalHeight ?? image.height;
        while (sourceWidth / 2 >= width) {
            const half = document.createElement("canvas");
            half.width = Math.ceil(sourceWidth / 2);
            half.height = Math.ceil(sourceHeight / 2);
            const context = half.getContext("2d");
            context.imageSmoothingQuality = "high";
            context.drawImage(source, 0, 0, half.width, half.height);
            source = half;
            sourceWidth = half.width;
            sourceHeight = half.height;
        }

        const result = document.createElement("canvas");
        result.width = width;
        result.height = height;
        const context = result.getContext("2d");
        context.imageSmoothingQuality = "high";
        context.drawImage(source, 0, 0, width, height);
        return result;
    }

    // Rings from the outside in, each turned around the centre by an angle that grows towards the middle, so the
    // corners stay put and the centre is turned `turns` times.
    function swirl(canvas, source, turns) {
        const context = canvas.getContext("2d");
        const centreX = canvas.width / 2;
        const centreY = canvas.height / 2;
        const radius = Math.hypot(centreX, centreY);
        context.drawImage(source, 0, 0);
        for (let ring = 0; ring < rings; ring++) {
            const r = radius * (1 - ring / rings);
            const towardsCentre = 1 - r / radius;
            context.save();
            context.beginPath();
            context.arc(centreX, centreY, r, 0, 2 * Math.PI);
            context.clip();
            context.translate(centreX, centreY);
            context.rotate(turns * 2 * Math.PI * towardsCentre * towardsCentre);
            context.translate(-centreX, -centreY);
            context.drawImage(source, 0, 0);
            context.restore();
        }
    }

    // Canvas blur filters are missing in Safari, so blur by shrinking to `blurWidth` and growing back in doublings;
    // one big enlargement would show the coarse pixel grid instead of a soft blur.
    function blur(canvas, blurWidth) {
        let small = scaled(canvas, blurWidth, Math.max(1, Math.round(blurWidth * canvas.height / canvas.width)));
        while (small.width * 2 < canvas.width) {
            const double = document.createElement("canvas");
            double.width = small.width * 2;
            double.height = small.height * 2;
            const context = double.getContext("2d");
            context.imageSmoothingQuality = "high";
            context.drawImage(small, 0, 0, double.width, double.height);
            small = double;
        }

        const context = canvas.getContext("2d");
        context.imageSmoothingQuality = "high";
        context.clearRect(0, 0, canvas.width, canvas.height);
        context.drawImage(small, 0, 0, canvas.width, canvas.height);
    }

    function draw(canvas, image, turns, blurWidth) {
        const width = detailWidth;
        const height = Math.max(1, Math.round(width * image.naturalHeight / image.naturalWidth));
        canvas.width = width;
        canvas.height = height;
        swirl(canvas, scaled(image, width, height), turns);
        if (blurWidth && blurWidth < width) {
            blur(canvas, blurWidth);
        }
    }

    window.obscureImage = async (canvas, url, turns, blurWidth) => {
        const key = `${turns}|${blurWidth}|${url}`;
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
            draw(canvas, image, turns, blurWidth);
        }
    };
})();
