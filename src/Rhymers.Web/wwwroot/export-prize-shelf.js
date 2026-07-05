// Экспорт полки призов в изображение
window.exportPrizeShelfAsImage = async function (userName) {
    try {
        const prizeShelfElement = document.getElementById('prizeShelf');
        if (!prizeShelfElement) {
            console.error('Prize shelf element not found');
            alert('Ошибка: элемент полки призов не найден');
            return;
        }

        // Создаем canvas из HTML элемента
        const canvas = await html2canvas(prizeShelfElement, {
            scale: 2,
            backgroundColor: '#ffffff',
            logging: false,
            allowTaint: true,
            useCORS: true,
            windowHeight: prizeShelfElement.scrollHeight,
            windowWidth: prizeShelfElement.scrollWidth
        });

        // Преобразуем canvas в blob и скачиваем
        canvas.toBlob(function (blob) {
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = `${userName}_prizes_${new Date().toISOString().split('T')[0]}.png`;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            URL.revokeObjectURL(url);
        }, 'image/png');
    } catch (error) {
        console.error('Export error:', error);
        alert('Ошибка при экспорте: ' + error.message);
    }
};
