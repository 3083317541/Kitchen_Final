const path = require('path');
const sharp = require('sharp');
const directory = path.resolve(__dirname, '../../Assets/_Game/Art/UI');

async function render() {
    for (const name of ['BreadPaperclip', 'BreadPaperclipEyes']) {
        await sharp(path.join(directory, name + '.svg'), { density: 384 })
            .png().toFile(path.join(directory, name + '.png'));
    }
}

render().catch(error => { console.error(error); process.exitCode = 1; });
