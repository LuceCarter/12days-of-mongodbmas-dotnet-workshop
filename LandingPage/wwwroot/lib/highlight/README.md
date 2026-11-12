# highlight.js (vendored)

Syntax highlighting for code blocks in the markdown the site renders.

| | |
|---|---|
| Version | 11.12.0 |
| Licence | BSD-3-Clause (see `LICENSE`) |
| Source | `https://cdn.jsdelivr.net/npm/@highlightjs/cdn-assets@11.12.0/es/` (the npm package `@highlightjs/cdn-assets`) |

Vendored rather than loaded from a CDN so the site has no third-party runtime dependency.
Only the grammars the repository's code blocks use are included — the list lives at the top of
`wwwroot/js/site.js`.

## Updating

Download the same files from the new version's `es/` folder, replace them, update the version
above and the checksums below, and look at a page with code on it. Do it on purpose, not by accident.

## SHA-256

```
67a339aa68880a40c9803def9c8cebb2225dec76230382db86e349370dee70e9  core.min.js
7db1758fe6f04cd410760c29879812a3774edc54edcc91323511a24efc0b9e61  languages/bash.min.js
74bb1a32b37d581173fc9d880ab5be6c7e448bbb12552c3e2a3ac7fc3c57cf8f  languages/csharp.min.js
c3dbd515a79e2c00ff4535aa5ad2fe539fb2587d047f9ab826107d0784da7bb6  languages/javascript.min.js
8f2343ffbcc4fdea4b336a4241d54d223d52f99b67729d2d6d3556bbf66908ae  languages/json.min.js
67c95a71dae92f010a15e959158aaf682cd850a008d466f1c5d40394cbe22f12  languages/powershell.min.js
79bead5cf2bfa93d3982073d000a97802f394fe287736644bab30f40cb5c7380  languages/sql.min.js
6c081431591d9df696c82dc598fe1423765b8a299b200ed00b281afd0f64c490  LICENSE
```
