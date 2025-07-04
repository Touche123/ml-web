let globalMatches = [];

window.storeMatches = (matches) => {
    globalMatches = matches;
}

window.drawOverlayFromBlazor = () => {
    const img = document.getElementById("mainImage");
    const canvas = document.getElementById("overlayCanvas");
    const ctx = canvas.getContext("2d");
    ctx.clearRect(0, 0, canvas.width, canvas.height);

    if (globalMatches.length === 0) {
        console.warn("No matches to draw.");
        return;
    }
    drawOverlay(globalMatches);
}

window.drawOverlay = (matches) => {
    const img = document.getElementById("mainImage");
    const canvas = document.getElementById("overlayCanvas");

    if (!img || !canvas) {
        console.warn("Image or canvas not found.");
        return;
    }

    if (!img.complete || img.naturalWidth === 0) {
        console.warn("Image not fully loaded yet.");
        return;
    }
    const imgWidth = img.clientWidth;
    const imgHeight = img.clientHeight;

    canvas.width = imgWidth;
    canvas.height = imgHeight;

    canvas.style.width = imgWidth + "px";
    canvas.style.height = imgHeight + "px"

    const ctx = canvas.getContext("2d");
    ctx.clearRect(0, 0, canvas.width, canvas.height);

    ctx.strokeStyle = "red";
    ctx.lineWidth = 2;
    ctx.font = "16px Arial";
    ctx.fillStyle = "rgba(255,0,0,0.3)";

    matches.forEach(m => {
        console.log("Match object:", m);
        if (!m.position || !m.templateSize) {
            console.warn("Invalid match:", m);
            return;
        }

        const scaleX = img.clientWidth / img.naturalWidth;
        const scaleY = img.clientHeight / img.naturalHeight;
        const angle = m.angle * -1 ?? 0; // Om du har angle i matchresultatet

        const x = m.position.x * scaleX; // stor bokstav X
        const y = m.position.y * scaleY; // stor bokstav Y
        const w = m.templateSize.width * scaleX; // stor bokstav Width
        const h = m.templateSize.height * scaleY; // stor bokstav Height

        console.log("Drawing box:", x, y, w, h);

        //ctx.fillRect(x, y, w, h);
        //ctx.strokeRect(x, y, w, h);
        drawRotatedRect(ctx, x, y, w, h, angle, m.Label);

        //ctx.fillStyle = "rgba(255,0,0,1)";
        //ctx.fillText(m.label ?? "Template", x + 0, y - 10);
        //ctx.fillStyle = "rgba(255,0,0,0.3)";
    });
};

function drawRotatedRect(ctx, x, y, width, height, angleDeg, label) {
    const angleRad = angleDeg * Math.PI / 180;

    ctx.save();

    ctx.translate(x + width / 2, y + height / 2);
    ctx.rotate(angleRad);
    
    ctx.fillRect(-width / 2, -height / 2, width, height);
    ctx.strokeRect(-width / 2, -height / 2, width, height);

    // Rita label
    //ctx.translate(x + width, y + height);
    ctx.fillStyle = "rgba(255,0,0,1)";
    ctx.fillText(label ?? "Template", -80, -80);

    ctx.restore();
}