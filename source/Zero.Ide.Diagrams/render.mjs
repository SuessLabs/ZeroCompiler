// Render Lite.StateMachine's exported DOT with Graphviz, without changing its topology.
import fs from 'node:fs/promises';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

const [rendererModule, destination = 'docs/images/ide-execution'] = process.argv.slice(2);
if (!rendererModule) throw new Error('Pass the path to @viz-js/viz/dist/viz.js.');
const { instance } = await import(pathToFileURL(path.resolve(rendererModule)).href);
const viz = await instance();
for (const mode of ['build', 'run', 'debug']) {
  const dot = await fs.readFile(path.join(destination, `${mode}.dot`), 'utf8');
  const svg = viz.renderString(dot, {
    format: 'svg', engine: 'dot',
    graphAttributes: { bgcolor: 'white', pad: '0.3', nodesep: '0.5', ranksep: '0.55', label: `Zero IDE — ${mode[0].toUpperCase()}${mode.slice(1)}`, labelloc: 't', fontname: 'Arial', fontsize: '20' },
    nodeAttributes: { fontname: 'Arial', fontsize: '12', color: '#64748B', fontcolor: '#0F172A', penwidth: '1.2', margin: '0.15,0.1' },
    edgeAttributes: { fontname: 'Arial', fontsize: '10', penwidth: '1.3' },
  });
  await fs.writeFile(path.join(destination, `${mode}.svg`), svg);
  console.log(path.join(destination, `${mode}.svg`));
}
