window.readJsonFile = function (inputRefId, dotNetHelper) {
    const input = document.getElementById(inputRefId);
    if (!input || !input.files || input.files.length === 0) {
        console.error("Ingen fil vald");
        return;
    }

    const file = input.files[0];
    const reader = new FileReader();
    reader.onload = () => {
        dotNetHelper.invokeMethodAsync('OnJsonLoaded', reader.result);
    };
    reader.onerror = (e) => {
        console.error("Läsfel:", e);
    };
    reader.readAsText(file);
};