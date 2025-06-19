window.chartInstance = null;

window.renderChart = (labels, scores) => {
    const canvas = document.getElementById('chart');
    if (!canvas) {
        console.error("❌ Kunde inte hitta canvas-elementet");
        return;
    }

    const ctx = canvas.getContext('2d');

    // 🧹 Ta bort tidigare graf om den finns
    if (window.chartInstance) {
        window.chartInstance.destroy();
    }

    // 🆕 Skapa ny graf
    window.chartInstance = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [{
                label: 'Confidence',
                data: scores,
                backgroundColor: 'rgba(54, 162, 235, 0.5)'
            }]
        }
    });
};