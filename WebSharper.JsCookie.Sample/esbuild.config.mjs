import { build } from 'esbuild'
import { readFileSync, writeFileSync, mkdirSync, copyFileSync } from 'fs'

const name = 'WebSharper.JsCookie.Sample'

// The WebSharper-generated module imports the npm `js-cookie` package (and the
// WebSharper runtime); bundle it all into a single IIFE for the static page.
await build({
  entryPoints: [`./build/${name}.js`],
  bundle: true,
  minify: true,
  format: 'iife',
  outfile: `../dist/Scripts/${name}.min.js`,
  globalName: 'wsbundle'
})

mkdirSync('../dist/css', { recursive: true })
copyFileSync('./css/index.css', '../dist/css/index.css')

// Fold the WebSharper head (linked resources) into the page. `js-cookie` is
// bundled from npm, so the head is normally empty, but this keeps the mechanism
// for any linked resources the binding may add later.
const head = readFileSync(`./build/${name}.head.html`, 'utf8')
const html = readFileSync('./index.html', 'utf8').replace('<!-- ws-head -->', head)
writeFileSync('../dist/index.html', html)

console.log('Bundled:', name)
