// File downloads of the creator (export package, project file). The bytes never leave the browser.

export function downloadFile(fileName, contentType, bytes) {
    const blob = new Blob([bytes], { type: contentType });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    link.style.display = "none";
    document.body.appendChild(link);
    link.click();
    link.remove();
    // Give the browser time to start the download before the URL is released.
    setTimeout(() => URL.revokeObjectURL(url), 10000);
}
